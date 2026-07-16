using BankApiApp.Data;
using BankApiApp.DTOs.Account;
using BankApiApp.DTOs.Auth;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BankApiApp.Services;

public class AuthService : IAuthService
{
    private readonly ILocalizationService _localizer;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;
    private readonly AppDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IEmailService _emailService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IConfiguration config,
        AppDbContext context,
        IAuditService auditService,
        IEmailService emailService,
        ILocalizationService localizer)   // ← YENI parametr
    {
        _userManager = userManager;
        _config = config;
        _context = context;
        _auditService = auditService;
        _emailService = emailService;
        _localizer = localizer;           // ← YENI setir
    }

    public async Task<string> RegisterAsync(RegisterDto dto)
    {
        var existing = await _userManager.FindByEmailAsync(dto.Email);
        if (existing != null)
            throw new BadRequestException(_localizer.Get("EmailAlreadyRegistered"));

        var user = new ApplicationUser
        {
            FullName = dto.FullName,
            Email = dto.Email,
            UserName = dto.Email
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            throw new BadRequestException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, "Customer");

        var code = GenerateCode();
        user.EmailConfirmationCode = code;
        user.EmailConfirmationCodeExpiry = DateTime.UtcNow.AddMinutes(15);
        await _userManager.UpdateAsync(user);

        await _emailService.SendAsync(
            user.Email!,
            "Email tesdiqi - BankApiApp",
            EmailTemplates.EmailConfirmation(user.FullName, code));

        await _auditService.LogManualAsync("Register", "Yeni qeydiyyat.", user.Id, user.Email);

        return "Qeydiyyat ugurludur. Email unvaniniza gonderilen kodu tesdiqleyin.";
    }

    public async Task<string> ConfirmEmailAsync(ConfirmEmailDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email)
            ?? throw new NotFoundException(_localizer.Get("UserNotFound"));

        if (user.EmailConfirmed)
            return "Email artiq tesdiqlenib.";

        if (user.EmailConfirmationCode != dto.Code)
            throw new BadRequestException(_localizer.Get("InvalidVerificationCode"));

        if (user.EmailConfirmationCodeExpiry < DateTime.UtcNow)
            throw new BadRequestException(_localizer.Get("VerificationCodeExpired"));

        user.EmailConfirmed = true;
        user.EmailConfirmationCode = null;
        user.EmailConfirmationCodeExpiry = null;
        await _userManager.UpdateAsync(user);

        await _auditService.LogManualAsync("EmailConfirmed", "Email tesdiqlendi.", user.Id, user.Email);

        return "Email ugurla tesdiqlendi. Indi daxil ola bilersiniz.";
    }

    public async Task<string> ResendConfirmationCodeAsync(ResendCodeDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user == null || user.EmailConfirmed)
            return "Eger email movcud ve tesdiqlenmeyibse, yeni kod gonderildi.";

        var code = GenerateCode();
        user.EmailConfirmationCode = code;
        user.EmailConfirmationCodeExpiry = DateTime.UtcNow.AddMinutes(15);
        await _userManager.UpdateAsync(user);

        await _emailService.SendAsync(
            user.Email!,
            "Email tesdiqi (yeni kod) - BankApiApp",
            EmailTemplates.EmailConfirmation(user.FullName, code));

        return "Eger email movcud ve tesdiqlenmeyibse, yeni kod gonderildi.";
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            await _auditService.LogManualAsync("LoginFailed", $"Movcud olmayan email: {dto.Email}");
            throw new BadRequestException(_localizer.Get("InvalidEmailOrPassword"));
        }

        var valid = await _userManager.CheckPasswordAsync(user, dto.Password);
        // Google ile yaradilmis hesabin parolu yoxdur — aydin mesaj verek
        if (!valid && !string.IsNullOrEmpty(user.GoogleId) && user.PasswordHash == null)
            throw new BadRequestException("Bu hesab Google ile yaradilib. 'Google ile davam et' duymesini isledin.");
        // bloklanmis istifadeci daxil ola bilmez
        if (await _userManager.IsLockedOutAsync(user))
        {
            await _auditService.LogManualAsync("LoginBlocked", "Bloklanmis hesab.", user.Id, user.Email);
            throw new BadRequestException(_localizer.Get("HesabnzbloklanbDstkllaqsaxlayn"));
        }
        if (!valid)
        {
            await _auditService.LogManualAsync("LoginFailed", "Yanlis parol.", user.Id, user.Email);
            throw new BadRequestException(_localizer.Get("InvalidEmailOrPassword"));
        }

        if (!user.EmailConfirmed)
        {
            await _auditService.LogManualAsync("LoginBlocked", "Tesdiqlenmemis email.", user.Id, user.Email);
            throw new BadRequestException(_localizer.Get("EmailNotVerified"));
        }

        if (user.IsTwoFactorEnabled)
        {
            var otp = GenerateCode();
            user.TwoFactorCode = otp;
            user.TwoFactorCodeExpiry = DateTime.UtcNow.AddMinutes(5);
            await _userManager.UpdateAsync(user);

            await _emailService.SendAsync(
                user.Email!,
                "Giris tesdiqi (OTP) - BankApiApp",
                EmailTemplates.TwoFactorCode(user.FullName, otp));

            await _auditService.LogManualAsync("TwoFactorRequested", "OTP gonderildi.", user.Id, user.Email);

            return new AuthResponseDto
            {
                RequiresTwoFactor = true,
                Message = "Email unvaniniza giris kodu gonderildi. Kodu daxil edin."
            };
        }

        await _auditService.LogManualAsync("Login", "Ugurlu giris.", user.Id, user.Email);
        return await BuildAuthResponseAsync(user);
    }

    public async Task<AuthResponseDto> VerifyTwoFactorAsync(VerifyTwoFactorDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email)
            ?? throw new BadRequestException(_localizer.Get("InvalidOrExpiredCode"));

        if (user.TwoFactorCode != dto.Code || user.TwoFactorCodeExpiry < DateTime.UtcNow)
        {
            await _auditService.LogManualAsync("TwoFactorFailed", "Yanlis OTP.", user.Id, user.Email);
            throw new BadRequestException(_localizer.Get("InvalidOrExpiredCode"));
        }

        user.TwoFactorCode = null;
        user.TwoFactorCodeExpiry = null;
        await _userManager.UpdateAsync(user);

        await _auditService.LogManualAsync("Login", "Ugurlu giris (2FA).", user.Id, user.Email);

        return await BuildAuthResponseAsync(user);
    }

    public async Task<string> ToggleTwoFactorAsync(string userId, ToggleTwoFactorDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(_localizer.Get("UserNotFound"));

        user.IsTwoFactorEnabled = dto.Enable;
        await _userManager.UpdateAsync(user);

        await _auditService.LogManualAsync(
            dto.Enable ? "TwoFactorEnabled" : "TwoFactorDisabled",
            dto.Enable ? "2FA aktivlesdirildi." : "2FA sonduruldu.",
            user.Id, user.Email);

        return dto.Enable
            ? "Iki merheleli dogrulama aktivlesdirildi."
            : "Iki merheleli dogrulama sonduruldu.";
    }

    // --- GOOGLE LOGIN (yeni) ---

    public async Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginDto dto)
    {
        // 1. Google ID token-i yoxla (imza + bizim Client ID-ye aid olub-olmadigi)
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["GoogleAuth:ClientId"] }
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, settings);
        }
        catch
        {
            throw new BadRequestException(_localizer.Get("InvalidGoogleToken"));
        }

        // 2. Token-den melumatlari cixar
        var email = payload.Email;
        var fullName = payload.Name ?? email;
        var googleId = payload.Subject;   // Google-un unikal istifadeci ID-si

        // 3. Istifadeci bazada var? (email uzre)
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            // YOXDUR → yeni yarat (parolsuz, email tesdiqli — Google onsuz tesdiqleyib)
            user = new ApplicationUser
            {
                FullName = fullName,
                Email = email,
                UserName = email,
                EmailConfirmed = true,      // Google emaili tesdiqleyib
                GoogleId = googleId
            };

            var result = await _userManager.CreateAsync(user);   // parol YOX
            if (!result.Succeeded)
                throw new BadRequestException(string.Join("; ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Customer");
            await _auditService.LogManualAsync("GoogleRegister", "Google ile qeydiyyat.", user.Id, user.Email);
        }
        else
        {
            // VAR → movcud hesaba Google-u bagla (eger hele bagli deyilse)
            if (string.IsNullOrEmpty(user.GoogleId))
            {
                user.GoogleId = googleId;
                user.EmailConfirmed = true;   // Google ile girirse email tesdiqlidir
                await _userManager.UpdateAsync(user);
            }
        }

        await _auditService.LogManualAsync("GoogleLogin", "Google ile giris.", user.Id, user.Email);

        // 4. Bizim oz JWT token-imizi ver
        return await BuildAuthResponseAsync(user);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto dto)
    {
        var stored = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken);

        if (stored == null || stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
            throw new BadRequestException(_localizer.Get("InvalidOrExpiredRefreshToken"));

        stored.IsRevoked = true;
        await _context.SaveChangesAsync();

        return await BuildAuthResponseAsync(stored.User);
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new NotFoundException(_localizer.Get("UserNotFound"));

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
            throw new BadRequestException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();
        foreach (var t in tokens)
            t.IsRevoked = true;
        await _context.SaveChangesAsync();

        await _auditService.LogManualAsync("PasswordChanged", "Parol deyisdirildi.", userId);
    }

    public async Task<string> ForgotPasswordAsync(ForgotPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user == null)
            return "Eger bu email qeydiyyatdadirsa, sifirlama kodu gonderildi.";

        var code = GenerateCode();
        user.PasswordResetCode = code;
        user.PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(15);
        await _userManager.UpdateAsync(user);

        await _emailService.SendAsync(
            user.Email!,
            "Parol sifirlama - BankApiApp",
            EmailTemplates.PasswordReset(user.FullName, code));

        await _auditService.LogManualAsync("PwdResetRequest", "Parol sifirlama teleb edildi.", user.Id, user.Email);

        return "Eger bu email qeydiyyatdadirsa, sifirlama kodu gonderildi.";
    }

    public async Task<string> ResetPasswordAsync(ResetPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email)
            ?? throw new BadRequestException(_localizer.Get("InvalidOrExpiredCode"));

        if (user.PasswordResetCode != dto.Code || user.PasswordResetCodeExpiry < DateTime.UtcNow)
            throw new BadRequestException(_localizer.Get("InvalidOrExpiredCode"));

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
        if (!result.Succeeded)
            throw new BadRequestException(string.Join("; ", result.Errors.Select(e => e.Description)));

        user.PasswordResetCode = null;
        user.PasswordResetCodeExpiry = null;
        await _userManager.UpdateAsync(user);

        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
            .ToListAsync();
        foreach (var t in tokens)
            t.IsRevoked = true;
        await _context.SaveChangesAsync();

        await _auditService.LogManualAsync("PasswordReset", "Parol sifirlandi.", user.Id, user.Email);

        return "Parol ugurla sifirlandi. Indi yeni parolla daxil ola bilersiniz.";
    }

    public async Task LogoutAsync(string userId, RefreshTokenRequestDto dto)
    {
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken && rt.UserId == userId);

        if (token != null && !token.IsRevoked)
        {
            token.IsRevoked = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<ProfileDto> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(_localizer.Get("UserNotFound"));

        var roles = await _userManager.GetRolesAsync(user);

        var accounts = await _context.Accounts
            .Where(a => a.OwnerId == userId)
            .ToListAsync();

        return new ProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            PhoneNumber = user.PhoneNumber,
            IsTwoFactorEnabled = user.IsTwoFactorEnabled,
            EmailConfirmed = user.EmailConfirmed,
            Roles = roles,
            AccountCount = accounts.Count,
            TotalBalance = accounts.Sum(a => a.Balance),
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<ProfileDto> UpdateProfileAsync(string userId, UpdateProfileDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(_localizer.Get("UserNotFound"));

        // ad
        var name = dto.FullName?.Trim();
        if (string.IsNullOrEmpty(name))
            throw new BadRequestException(_localizer.Get("FullNameRequired"));
        user.FullName = name;

        // telefon (opsional)
        var phone = dto.PhoneNumber?.Trim();
        user.PhoneNumber = string.IsNullOrEmpty(phone) ? null : phone;

        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new BadRequestException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _auditService.LogManualAsync("ProfileUpdated", "Profil yenilendi.", user.Id, user.Email);

        return await GetProfileAsync(userId);
    }

    // --- komekci metodlar ---

    private async Task<AuthResponseDto> BuildAuthResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var expiryMinutes = int.Parse(_config["Jwt:ExpiryMinutes"]!);
        var expires = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var accessToken = CreateAccessToken(user, roles, expires);
        var refreshToken = GenerateRefreshToken();

        _context.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        });
        await _context.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = accessToken,
            RefreshToken = refreshToken,
            Email = user.Email!,
            FullName = user.FullName,
            ExpiresAt = expires,
            RequiresTwoFactor = false
        };
    }

    private string CreateAccessToken(ApplicationUser user, IList<string> roles, DateTime expires)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.FullName),
            new("SecurityStamp", user.SecurityStamp ?? string.Empty)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string GenerateCode()
    {
        return Random.Shared.Next(100000, 999999).ToString();
    }
}