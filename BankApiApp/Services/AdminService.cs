using BankApiApp.Data; // AppDbContext üçün lazımdır
using BankApiApp.DTOs.Admin;
using BankApiApp.DTOs.Common;
using BankApiApp.Exceptions;
using BankApiApp.Extensions;
using BankApiApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class AdminService : IAdminService
{
    private readonly ILocalizationService _localizer;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;

    public AdminService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext context,
        INotificationService notificationService,
        ILocalizationService localizer) // [DÜZƏLİŞ]: localizer inject edildi
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _notificationService = notificationService;
        _localizer = localizer;
    }

    // istifadeci siyahisi (axtaris + filtrle)
    public async Task<PagedResult<UserDto>> GetUsersAsync(
        PaginationParams pagination, string? search, bool? blocked)
    {
        var query = _userManager.Users.AsQueryable();

        // ad ve ya email uzre axtaris
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(u =>
                u.Email!.ToLower().Contains(s) ||
                u.FullName.ToLower().Contains(s));
        }

        // yalniz bloklu / yalniz aciq
        if (blocked.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            query = blocked.Value
                ? query.Where(u => u.LockoutEnd != null && u.LockoutEnd > now)
                : query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= now);
        }

        var ordered = query.OrderBy(u => u.Email);
        var paged = await ordered.ToPagedResultAsync(pagination);

        // rollari yukle
        var items = new List<UserDto>();
        foreach (var user in paged.Items)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                EmailConfirmed = user.EmailConfirmed,
                IsBlocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow,
                Roles = roles
            });
        }

        return new PagedResult<UserDto>
        {
            Items = items,
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages
        };
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

    // bank statistikasi — admin dashboard ucun
    public async Task<AdminStatsDto> GetStatsAsync()
    {
        var now = DateTime.UtcNow;
        var todayStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var weekStart = todayStart.AddDays(-7);
        var monthStart = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1), DateTimeKind.Utc);

        // emeliyyat statistikasi (tamamlanmislar)
        var completed = _context.Transactions.Where(t => t.Status == TransactionStatus.Completed);

        var todayCount = await completed.CountAsync(t => t.CreatedAt >= todayStart);
        var todayVolume = await completed.Where(t => t.CreatedAt >= todayStart)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        var weekCount = await completed.CountAsync(t => t.CreatedAt >= weekStart);
        var weekVolume = await completed.Where(t => t.CreatedAt >= weekStart)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        var monthCount = await completed.CountAsync(t => t.CreatedAt >= monthStart);
        var monthVolume = await completed.Where(t => t.CreatedAt >= monthStart)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        // umumi gostericiler
        var totalUsers = await _userManager.Users.CountAsync();
        var blockedUsers = await _userManager.Users
            .CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
        var totalAccounts = await _context.Accounts.CountAsync();
        var totalBalance = await _context.Accounts.SumAsync(a => (decimal?)a.Balance) ?? 0m;

        // en aktiv 5 istifadeci (bu ay, cixan emeliyyatlara gore)
        var topUsers = await _context.Transactions
            .Where(t => t.Status == TransactionStatus.Completed
                     && t.CreatedAt >= monthStart
                     && t.FromAccountId != null)
            .Include(t => t.FromAccount).ThenInclude(a => a!.Owner)
            .GroupBy(t => t.FromAccount!.Owner!.Email)
            .Select(g => new TopUserDto
            {
                Email = g.Key!,
                TransactionCount = g.Count(),
                TotalVolume = g.Sum(t => t.Amount)
            })
            .OrderByDescending(x => x.TransactionCount)
            .Take(5)
            .ToListAsync();

        // provayder uzre kommunal odenis hecmi (bu ay)
        var utilityByProvider = await _context.UtilityPayments
            .Where(p => p.CreatedAt >= monthStart)
            .Include(p => p.Provider)
            .GroupBy(p => p.Provider.Name)
            .Select(g => new ProviderVolumeDto
            {
                ProviderName = g.Key,
                PaymentCount = g.Count(),
                TotalAmount = g.Sum(p => p.Amount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToListAsync();

        return new AdminStatsDto
        {
            TodayTransactionCount = todayCount,
            TodayTransactionVolume = todayVolume,
            WeekTransactionCount = weekCount,
            WeekTransactionVolume = weekVolume,
            MonthTransactionCount = monthCount,
            MonthTransactionVolume = monthVolume,
            TotalUsers = totalUsers,
            BlockedUsers = blockedUsers,
            TotalAccounts = totalAccounts,
            TotalBalance = totalBalance,
            TopActiveUsers = topUsers,
            UtilityByProvider = utilityByProvider
        };
    }

    // hesabi dondur (subheli fealiyyet) - user blokundan ferqli, yalniz bu hesab
    public async Task<string> FreezeAccountAsync(Guid accountId, string? reason)
    {
        var account = await _context.Accounts
            .Include(a => a.Owner)
            .FirstOrDefaultAsync(a => a.Id == accountId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        if (account.Status == AccountStatus.Blocked)
            throw new BadRequestException("Hesab artiq dondurulub.");

        if (account.Status == AccountStatus.Closed)
            throw new BadRequestException("Bagli hesab dondurula bilmez.");

        account.Status = AccountStatus.Blocked;
        await _context.SaveChangesAsync();

        // sahibe bildiris (varsa notification service)
        var msg = string.IsNullOrEmpty(reason)
            ? "Hesabiniz subheli fealiyyet sebebiyle dondurulub. Destekle elaqe saxlayin."
            : $"Hesabiniz dondurulub. Sebeb: {reason}";
        await _notificationService.SendToUserAsync(account.OwnerId,
            "Hesab donduruldu", msg, "System");

        return $"{account.AccountNumber} nomreli hesab dondurudu.";
    }

    // hesabi geri ac
    public async Task<string> UnfreezeAccountAsync(Guid accountId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        if (account.Status != AccountStatus.Blocked)
            throw new BadRequestException("Hesab dondurulmuş veziyyetde deyil.");

        account.Status = AccountStatus.Active;
        await _context.SaveChangesAsync();

        await _notificationService.SendToUserAsync(account.OwnerId,
            "Hesab aktivlesdirildi", "Hesabiniz yeniden aktivdir.", "System");

        return $"{account.AccountNumber} nomreli hesab yeniden aktivdir.";
    }
}