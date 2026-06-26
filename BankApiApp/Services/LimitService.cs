using BankApiApp.Data;
using BankApiApp.DTOs.Limits;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class LimitService : ILimitService
{
    private readonly AppDbContext _context;

    // standart limitlər (hesaba özəl limit qoyulmayıbsa bunlar tətbiq olunur)
    public const decimal DefaultDailyLimit = 2000m;
    public const decimal DefaultMonthlyLimit = 20000m;
    public const decimal DefaultYearlyLimit = 200000m;

    public LimitService(AppDbContext context)
    {
        _context = context;
    }

    // çıxarış/köçürmə öncəsi limit yoxlaması
    public async Task EnsureWithinLimitsAsync(Account account, decimal amount)
    {
        var (spentToday, spentMonth, spentYear) = await GetSpentAsync(account.Id);

        var daily = account.DailyLimit ?? DefaultDailyLimit;
        var monthly = account.MonthlyLimit ?? DefaultMonthlyLimit;
        var yearly = account.YearlyLimit ?? DefaultYearlyLimit;

        if (spentToday + amount > daily)
            throw new BadRequestException(
                $"Günlük limit aşıldı. Limit: {daily}, bu gün istifadə: {spentToday}, qalıq: {Math.Max(0, daily - spentToday)}.");

        if (spentMonth + amount > monthly)
            throw new BadRequestException(
                $"Aylıq limit aşıldı. Limit: {monthly}, bu ay istifadə: {spentMonth}, qalıq: {Math.Max(0, monthly - spentMonth)}.");

        if (spentYear + amount > yearly)
            throw new BadRequestException(
                $"İllik limit aşıldı. Limit: {yearly}, bu il istifadə: {spentYear}, qalıq: {Math.Max(0, yearly - spentYear)}.");
    }

    public async Task<LimitInfoDto> GetLimitInfoAsync(string userId, Guid accountId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId)
            ?? throw new NotFoundException("Hesab tapılmadı.");

        var (spentToday, spentMonth, spentYear) = await GetSpentAsync(account.Id);

        var daily = account.DailyLimit ?? DefaultDailyLimit;
        var monthly = account.MonthlyLimit ?? DefaultMonthlyLimit;
        var yearly = account.YearlyLimit ?? DefaultYearlyLimit;

        return new LimitInfoDto
        {
            DailyLimit = daily,
            DailyUsed = spentToday,
            DailyRemaining = Math.Max(0, daily - spentToday),
            MonthlyLimit = monthly,
            MonthlyUsed = spentMonth,
            MonthlyRemaining = Math.Max(0, monthly - spentMonth),
            YearlyLimit = yearly,
            YearlyUsed = spentYear,
            YearlyRemaining = Math.Max(0, yearly - spentYear),
            IsCustom = account.DailyLimit.HasValue || account.MonthlyLimit.HasValue || account.YearlyLimit.HasValue
        };
    }

    public async Task<LimitInfoDto> UpdateLimitsAsync(string userId, Guid accountId, UpdateLimitsDto dto)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId)
            ?? throw new NotFoundException("Hesab tapılmadı.");

        if (dto.DailyLimit < 0 || dto.MonthlyLimit < 0 || dto.YearlyLimit < 0)
            throw new BadRequestException("Limit mənfi ola bilməz.");

        account.DailyLimit = dto.DailyLimit;
        account.MonthlyLimit = dto.MonthlyLimit;
        account.YearlyLimit = dto.YearlyLimit;

        await _context.SaveChangesAsync();

        return await GetLimitInfoAsync(userId, accountId);
    }

    // --- köməkçi: bu hesabdan gün/ay/il ərzində çıxan (xərclənən) məbləğlər ---
    private async Task<(decimal today, decimal month, decimal year)> GetSpentAsync(Guid accountId)
    {
        var now = DateTime.UtcNow;
        var dayStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var monthStart = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1), DateTimeKind.Utc);
        var yearStart = DateTime.SpecifyKind(new DateTime(now.Year, 1, 1), DateTimeKind.Utc);

        // çıxan (outgoing) tamamlanmış əməliyyatlar
        var outgoing = _context.Transactions
            .Where(t => t.FromAccountId == accountId && t.Status == TransactionStatus.Completed);

        var today = await outgoing.Where(t => t.CreatedAt >= dayStart).SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var month = await outgoing.Where(t => t.CreatedAt >= monthStart).SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var year = await outgoing.Where(t => t.CreatedAt >= yearStart).SumAsync(t => (decimal?)t.Amount) ?? 0m;

        return (today, month, year);
    }
}