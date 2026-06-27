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
        IEmailService emailService)
    {
        _userManager = userManager;
        _config = config;
        _context = context;
        _auditService = auditService;
        _emailService = emailService;
    }

    public async Task<string> RegisterAsync(RegisterDto dto)
    {
        var existing = await _userManager.FindByEmailAsync(dto.Email);
        if (existing != null)
            throw new BadRequestException("Bu email artıq qeydiyyatdan keçib.");

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
            "Email təsdiqi - BankApiApp",
            EmailTemplates.EmailConfirmation(user.FullName, code));

        await _auditService.LogManualAsync("Register", "Yeni qeydiyyat.", user.Id, user.Email);

        return "Qeydiyyat uğurludur. Email ünvanınıza göndərilən kodu təsdiqləyin.";
    }

    public async Task<string> ConfirmEmailAsync(ConfirmEmailDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        if (user.EmailConfirmed)
            return "Email artıq təsdiqlənib.";

        if (user.EmailConfirmationCode != dto.Code)
            throw new BadRequestException("Təsdiq kodu yanlışdır.");

        if (user.EmailConfirmationCodeExpiry < DateTime.UtcNow)
            throw new BadRequestException("Təsdiq kodunun vaxtı bitib. Yenidən kod tələb edin.");

        user.EmailConfirmed = true;
        user.EmailConfirmationCode = null;
        user.EmailConfirmationCodeExpiry = null;
        await _userManager.UpdateAsync(user);

        await _auditService.LogManualAsync("EmailConfirmed", "Email təsdiqləndi.", user.Id, user.Email);

        return "Email uğurla təsdiqləndi. İndi daxil ola bilərsiniz.";
    }

    public async Task<string> ResendConfirmationCodeAsync(ResendCodeDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user == null || user.EmailConfirmed)
            return "Əgər email mövcud və təsdiqlənməyibsə, yeni kod göndərildi.";

        var code = GenerateCode();
        user.EmailConfirmationCode = code;
        user.EmailConfirmationCodeExpiry = DateTime.UtcNow.AddMinutes(15);
        await _userManager.UpdateAsync(user);

        await _emailService.SendAsync(
            user.Email!,
            "Email təsdiqi (yeni kod) - BankApiApp",
            EmailTemplates.EmailConfirmation(user.FullName, code));

        return "Əgər email mövcud və təsdiqlənməyibsə, yeni kod göndərildi.";
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            await _auditService.LogManualAsync("LoginFailed", $"Mövcud olmayan email: {dto.Email}");
            throw new BadRequestException("Email və ya parol yanlışdır.");
        }

        var valid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!valid)
        {
            await _auditService.LogManualAsync("LoginFailed", "Yanlış parol.", user.Id, user.Email);
            throw new BadRequestException("Email və ya parol yanlışdır.");
        }

        if (!user.EmailConfirmed)
        {
            await _auditService.LogManualAsync("LoginBlocked", "Təsdiqlənməmiş email.", user.Id, user.Email);
            throw new BadRequestException("Email təsdiqlənməyib. Zəhmət olmasa əvvəlcə emailinizi təsdiqləyin.");
        }

        if (user.IsTwoFactorEnabled)
        {
            var otp = GenerateCode();
            user.TwoFactorCode = otp;
            user.TwoFactorCodeExpiry = DateTime.UtcNow.AddMinutes(5);
            await _userManager.UpdateAsync(user);

            await _emailService.SendAsync(
                user.Email!,
                "Giriş təsdiqi (OTP) - BankApiApp",
                EmailTemplates.TwoFactorCode(user.FullName, otp));

            await _auditService.LogManualAsync("TwoFactorRequested", "OTP göndərildi.", user.Id, user.Email);

            return new AuthResponseDto
            {
                RequiresTwoFactor = true,
                Message = "Email ünvanınıza giriş kodu göndərildi. Kodu daxil edin."
            };
        }

        await _auditService.LogManualAsync("Login", "Uğurlu giriş.", user.Id, user.Email);
        return await BuildAuthResponseAsync(user);
    }

    public async Task<AuthResponseDto> VerifyTwoFactorAsync(VerifyTwoFactorDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email)
            ?? throw new BadRequestException("Kod yanlışdır və ya vaxtı bitib.");

        if (user.TwoFactorCode != dto.Code || user.TwoFactorCodeExpiry < DateTime.UtcNow)
        {
            await _auditService.LogManualAsync("TwoFactorFailed", "Yanlış OTP.", user.Id, user.Email);
            throw new BadRequestException("Kod yanlışdır və ya vaxtı bitib.");
        }

        user.TwoFactorCode = null;
        user.TwoFactorCodeExpiry = null;
        await _userManager.UpdateAsync(user);

        await _auditService.LogManualAsync("Login", "Uğurlu giriş (2FA).", user.Id, user.Email);

        return await BuildAuthResponseAsync(user);
    }

    public async Task<string> ToggleTwoFactorAsync(string userId, ToggleTwoFactorDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        user.IsTwoFactorEnabled = dto.Enable;
        await _userManager.UpdateAsync(user);

        await _auditService.LogManualAsync(
            dto.Enable ? "TwoFactorEnabled" : "TwoFactorDisabled",
            dto.Enable ? "2FA aktivləşdirildi." : "2FA söndürüldü.",
            user.Id, user.Email);

        return dto.Enable
            ? "İki mərhələli doğrulama aktivləşdirildi."
            : "İki mərhələli doğrulama söndürüldü.";
    }

    // --- GOOGLE LOGIN (yeni) ---

    public async Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginDto dto)
    {
        // 1. Google ID token-i yoxla (imza + bizim Client ID-yə aid olub-olmadığı)
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
            throw new BadRequestException("Google token etibarsızdır.");
        }

        // 2. Token-dən məlumatları çıxar
        var email = payload.Email;
        var fullName = payload.Name ?? email;
        var googleId = payload.Subject;   // Google-un unikal istifadəçi ID-si

        // 3. İstifadəçi bazada var? (email üzrə)
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            // YOXDUR → yeni yarat (parolsuz, email təsdiqli — Google onsuz təsdiqləyib)
            user = new ApplicationUser
            {
                FullName = fullName,
                Email = email,
                UserName = email,
                EmailConfirmed = true,      // Google emaili təsdiqləyib
                GoogleId = googleId
            };

            var result = await _userManager.CreateAsync(user);   // parol YOX
            if (!result.Succeeded)
                throw new BadRequestException(string.Join("; ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Customer");
            await _auditService.LogManualAsync("GoogleRegister", "Google ilə qeydiyyat.", user.Id, user.Email);
        }
        else
        {
            // VAR → mövcud hesaba Google-u bağla (əgər hələ bağlı deyilsə)
            if (string.IsNullOrEmpty(user.GoogleId))
            {
                user.GoogleId = googleId;
                user.EmailConfirmed = true;   // Google ilə girirsə email təsdiqlidir
                await _userManager.UpdateAsync(user);
            }
        }

        await _auditService.LogManualAsync("GoogleLogin", "Google ilə giriş.", user.Id, user.Email);

        // 4. Bizim öz JWT token-imizi ver
        return await BuildAuthResponseAsync(user);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto dto)
    {
        var stored = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken);

        if (stored == null || stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
            throw new BadRequestException("Refresh token etibarsızdır və ya vaxtı bitib.");

        stored.IsRevoked = true;
        await _context.SaveChangesAsync();

        return await BuildAuthResponseAsync(stored.User);
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
            throw new BadRequestException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();
        foreach (var t in tokens)
            t.IsRevoked = true;
        await _context.SaveChangesAsync();

        await _auditService.LogManualAsync("PasswordChanged", "Parol dəyişdirildi.", userId);
    }

    public async Task<string> ForgotPasswordAsync(ForgotPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user == null)
            return "Əgər bu email qeydiyyatdadırsa, sıfırlama kodu göndərildi.";

        var code = GenerateCode();
        user.PasswordResetCode = code;
        user.PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(15);
        await _userManager.UpdateAsync(user);

        await _emailService.SendAsync(
            user.Email!,
            "Parol sıfırlama - BankApiApp",
            EmailTemplates.PasswordReset(user.FullName, code));

        await _auditService.LogManualAsync("PwdResetRequest", "Parol sıfırlama tələb edildi.", user.Id, user.Email);

        return "Əgər bu email qeydiyyatdadırsa, sıfırlama kodu göndərildi.";
    }

    public async Task<string> ResetPasswordAsync(ResetPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email)
            ?? throw new BadRequestException("Kod yanlışdır və ya vaxtı bitib.");

        if (user.PasswordResetCode != dto.Code || user.PasswordResetCodeExpiry < DateTime.UtcNow)
            throw new BadRequestException("Kod yanlışdır və ya vaxtı bitib.");

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

        await _auditService.LogManualAsync("PasswordReset", "Parol sıfırlandı.", user.Id, user.Email);

        return "Parol uğurla sıfırlandı. İndi yeni parolla daxil ola bilərsiniz.";
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
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        var roles = await _userManager.GetRolesAsync(user);

        var accounts = await _context.Accounts
            .Where(a => a.OwnerId == userId)
            .ToListAsync();

        return new ProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            Roles = roles,
            AccountCount = accounts.Count,
            TotalBalance = accounts.Sum(a => a.Balance)
        };
    }

    // --- köməkçi metodlar ---

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