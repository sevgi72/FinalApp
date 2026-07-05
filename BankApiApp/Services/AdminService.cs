using BankApiApp.Data; // AppDbContext üçün lazımdır
using BankApiApp.DTOs.Admin;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class AdminService : IAdminService
{
    private readonly ILocalizationService _localizer;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _context; // [DÜZƏLİŞ]: Çatışmayan DbContext əlavə edildi

    public AdminService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext context) // [DÜZƏLİŞ]: Konstruktora inject edildi
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    public async Task<List<UserDto>> GetUsersAsync()
    {
        var users = await _userManager.Users.ToListAsync();
        var result = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                Roles = roles
            });
        }
        return result;
    }

    public async Task AssignRoleAsync(string userId, string role)
    {
        if (role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException(_localizer.Get("SuperAdminRoleCannotBeManaged"));

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(_localizer.Get("UserNotFound"));

        if (!await _roleManager.RoleExistsAsync(role))
            throw new NotFoundException($"'{role}' adli rol movcud deyil.");

        if (await _userManager.IsInRoleAsync(user, role))
            throw new BadRequestException(_localizer.Get("UserAlreadyInRole"));

        var result = await _userManager.AddToRoleAsync(user, role);
        if (!result.Succeeded)
            throw new BadRequestException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task RemoveRoleAsync(string userId, string role)
    {
        if (role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException(_localizer.Get("SuperAdminRoleCannotBeManaged"));

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(_localizer.Get("UserNotFound"));

        var result = await _userManager.RemoveFromRoleAsync(user, role);
        if (!result.Succeeded)
            throw new BadRequestException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task<string> BlockUserAsync(string callerUserId, string targetUserId)
    {
        var target = await _userManager.FindByIdAsync(targetUserId)
            ?? throw new NotFoundException(_localizer.Get("İstifaditaplmad"));

        if (callerUserId == targetUserId)
            throw new BadRequestException(_localizer.Get("zhesabnzbloklayabilmzsiniz"));

        var targetRoles = await _userManager.GetRolesAsync(target);
        var caller = await _userManager.FindByIdAsync(callerUserId);
        var callerRoles = await _userManager.GetRolesAsync(caller!);

        bool targetIsPrivileged = targetRoles.Contains("Admin") || targetRoles.Contains("SuperAdmin");
        bool callerIsSuper = callerRoles.Contains("SuperAdmin");

        if (targetIsPrivileged && !callerIsSuper)
            throw new BadRequestException(_localizer.Get("AdminhesablarnyalnzSuperAdminidaredbilr"));

        if (targetRoles.Contains("SuperAdmin"))
            throw new BadRequestException(_localizer.Get("SuperAdminbloklanabilmz"));

        await _userManager.SetLockoutEnabledAsync(target, true);
        await _userManager.SetLockoutEndDateAsync(target, DateTimeOffset.UtcNow.AddYears(100));

        // Aktiv tokenləri ləğv et
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == targetUserId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var t in tokens)
            t.IsRevoked = true;

        await _context.SaveChangesAsync();

        return $"{target.Email} bloklandı.";
    }

    public async Task<string> UnblockUserAsync(string callerUserId, string targetUserId)
    {
        var target = await _userManager.FindByIdAsync(targetUserId)
            ?? throw new NotFoundException(_localizer.Get("İstifaditaplmad"));

        var targetRoles = await _userManager.GetRolesAsync(target);
        var caller = await _userManager.FindByIdAsync(callerUserId);
        var callerRoles = await _userManager.GetRolesAsync(caller!);

        bool targetIsPrivileged = targetRoles.Contains("Admin") || targetRoles.Contains("SuperAdmin");
        if (targetIsPrivileged && !callerRoles.Contains("SuperAdmin"))
            throw new BadRequestException(_localizer.Get("AdminhesablarnyalnzSuperAdminidaredbilr"));

        await _userManager.SetLockoutEndDateAsync(target, null);

        return $"{target.Email} blokdan çıxarıldı.";
    }

    public async Task<UserDetailDto> GetUserDetailAsync(string targetUserId)
    {
        var user = await _userManager.FindByIdAsync(targetUserId)
            ?? throw new NotFoundException(_localizer.Get("İstifaditaplmad"));

        var roles = await _userManager.GetRolesAsync(user);

        var accounts = await _context.Accounts
            .Where(a => a.OwnerId == targetUserId)
            .ToListAsync();

        var accountIds = accounts.Select(a => a.Id).ToList();

        var recentTx = await _context.Transactions
            .Where(t => (t.FromAccountId != null && accountIds.Contains(t.FromAccountId.Value))
                     || (t.ToAccountId != null && accountIds.Contains(t.ToAccountId.Value)))
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .ToListAsync();

        return new UserDetailDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            EmailConfirmed = user.EmailConfirmed,
            IsBlocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow,
            Roles = roles,
            CreatedAt = user.CreatedAt,
            AccountCount = accounts.Count,
            TotalBalance = accounts.Sum(a => a.Balance),
            Accounts = accounts.Select(a => new UserAccountSummaryDto
            {
                Id = a.Id,
                AccountNumber = a.AccountNumber,
                Balance = a.Balance,
                Status = a.Status.ToString()
            }).ToList(),
            RecentTransactions = recentTx.Select(t => new UserTransactionSummaryDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Type = t.Type.ToString(),
                Description = t.Description,
                CreatedAt = t.CreatedAt
            }).ToList()
        };
    }
}