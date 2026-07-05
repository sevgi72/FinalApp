using BankApiApp.Data;
using BankApiApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class CleanupService : ICleanupService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;
    private readonly ILogger<CleanupService> _logger;

    // təsdiqlənməmiş hesab neçə saatdan sonra silinsin
    private const int ExpiryHours = 24;

    public CleanupService(
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        ILogger<CleanupService> logger)
    {
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    public async Task CleanupUnconfirmedAccountsAsync()
    {
        var cutoff = DateTime.UtcNow.AddHours(-ExpiryHours);

        // tesdiqlenmemis VE kohne istifadeciler
        var staleUsers = await _userManager.Users
            .Where(u => !u.EmailConfirmed && u.CreatedAt < cutoff)
            .ToListAsync();

        if (!staleUsers.Any())
        {
            _logger.LogInformation("Temizleme: silinecek tesdiqlenmemis hesab yoxdur.");
            return;
        }

        int deleted = 0;

        foreach (var user in staleUsers)
        {
            // hesabi olan istifadecini SILME (tehlukesizlik — pul ola biler)
            var hasAccounts = await _context.Accounts.AnyAsync(a => a.OwnerId == user.Id);
            if (hasAccounts)
            {
                _logger.LogInformation("Temizleme: {Email} atlandi (hesabi var).", user.Email);
                continue;
            }

            // istifadeciye bagli refresh token-leri sil (varsa)
            var tokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == user.Id)
                .ToListAsync();
            if (tokens.Any())
            {
                _context.RefreshTokens.RemoveRange(tokens);
                await _context.SaveChangesAsync();
            }

            // istifadecini sil
            await _userManager.DeleteAsync(user);
            deleted++;

            _logger.LogInformation("Temizleme: tesdiqlenmemis hesab silindi — {Email}", user.Email);
        }

        _logger.LogInformation("Temizleme tamamlandi: {Count} hesab silindi.", deleted);
    }
}