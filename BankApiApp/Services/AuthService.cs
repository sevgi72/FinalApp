using BankApiApp.Data;
using BankApiApp.DTOs.Account;
using BankApiApp.DTOs.Auth;
using BankApiApp.Exceptions;
using BankApiApp.Models;
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

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IConfiguration config,
        AppDbContext context,
        IAuditService auditService)
    {
        _userManager = userManager;
        _config = config;
        _context = context;
        _auditService = auditService;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
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

        return await BuildAuthResponseAsync(user);
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

        await _auditService.LogManualAsync("Login", "Uğurlu giriş.", user.Id, user.Email);

        return await BuildAuthResponseAsync(user);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto dto)
    {
        var stored = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken);

        if (stored == null || stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
            throw new BadRequestException("Refresh token etibarsızdır və ya vaxtı bitib.");

        // köhnə refresh token-i ləğv et (rotation)
        stored.IsRevoked = true;
        await _context.SaveChangesAsync();

        // yeni access + refresh token ver
        return await BuildAuthResponseAsync(stored.User);
    }

    // --- köməkçi metodlar ---

    private async Task<AuthResponseDto> BuildAuthResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var expiryMinutes = int.Parse(_config["Jwt:ExpiryMinutes"]!);
        var expires = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var accessToken = CreateAccessToken(user, roles, expires);
        var refreshToken = GenerateRefreshToken();

        // refresh token-i bazada saxla
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
            ExpiresAt = expires
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

    public async Task ChangePasswordAsync(string userId, ChangePasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        // Identity köhnə parolu özü yoxlayır, yenisini hash-ləyir
        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
            throw new BadRequestException(string.Join("; ", result.Errors.Select(e => e.Description)));

        // Təhlükəsizlik: bütün köhnə refresh token-ləri ləğv et (köhnə sessiyalar bitsin)
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();
        foreach (var t in tokens)
            t.IsRevoked = true;
        await _context.SaveChangesAsync();

        await _auditService.LogManualAsync("PasswordChanged", "Parol dəyişdirildi.", userId);
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
        // token tapılmasa belə xəta vermirik — nəticə eynidir: sessiya bağlıdır
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
}