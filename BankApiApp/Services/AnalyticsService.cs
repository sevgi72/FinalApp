using BankApiApp.Data;
using BankApiApp.DTOs.Analytics;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _context;

    public AnalyticsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardDto> GetDashboardAsync(string userId)
    {
        // 1. Istifadecinin hesablari
        var accounts = await _context.Accounts
            .Where(a => a.OwnerId == userId)
            .ToListAsync();

        var accountIds = accounts.Select(a => a.Id).ToHashSet();
        var accountIdsNullable = accounts.Select(a => (Guid?)a.Id).ToList();
        var totalBalance = accounts.Sum(a => a.Balance);

        // 2. Bu hesablara aid tamamlanmis emeliyyatlar
        var transactions = await _context.Transactions
            .Where(t => t.Status == TransactionStatus.Completed &&
                        (accountIdsNullable.Contains(t.FromAccountId) ||
                         accountIdsNullable.Contains(t.ToAccountId)))
            .ToListAsync();

        // 3. Gelir / xerc
        decimal income = 0;
        decimal expense = 0;
        foreach (var t in transactions)
        {
            if (t.ToAccountId.HasValue && accountIds.Contains(t.ToAccountId.Value))
                income += t.Amount;     // pul sene geldi
            if (t.FromAccountId.HasValue && accountIds.Contains(t.FromAccountId.Value))
                expense += t.Amount;    // pul senden cixdi
        }

        // 4. Ayliq bolgu
        var monthly = transactions
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlySummaryDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Label = $"{g.Key.Year}-{g.Key.Month:D2}",
                Income = g.Where(t => t.ToAccountId.HasValue && accountIds.Contains(t.ToAccountId.Value))
                          .Sum(t => t.Amount),
                Expense = g.Where(t => t.FromAccountId.HasValue && accountIds.Contains(t.FromAccountId.Value))
                           .Sum(t => t.Amount)
            })
            .ToList();

        // 5. Tip uzre (Deposit / Withdraw / Transfer)
        var byType = transactions
            .GroupBy(t => t.Type)
            .Select(g => new TypeSummaryDto
            {
                Type = g.Key.ToString(),
                Total = g.Sum(t => t.Amount),
                Count = g.Count()
            })
            .ToList();

        return new DashboardDto
        {
            TotalBalance = totalBalance,
            AccountCount = accounts.Count,
            TotalIncome = income,
            TotalExpense = expense,
            NetFlow = income - expense,
            TransactionCount = transactions.Count,
            MonthlyBreakdown = monthly,
            ByType = byType
        };
    }

    public async Task<DateRangeSummaryDto> GetSummaryAsync(
    string userId, DateTime? startDate, DateTime? endDate)
    {
        // Tarix araligi: verilmeyibse son 30 gun
        var end = endDate ?? DateTime.UtcNow;
        var start = startDate ?? end.AddDays(-30);

        // PostgreSQL ucun UTC-e cevir + gunun evveli/sonu
        start = DateTime.SpecifyKind(start.Date, DateTimeKind.Utc);
        end = DateTime.SpecifyKind(end.Date, DateTimeKind.Utc).AddDays(1).AddTicks(-1);

        // Istifadecinin hesablari
        var accounts = await _context.Accounts
            .Where(a => a.OwnerId == userId)
            .ToListAsync();

        var accountIds = accounts.Select(a => a.Id).ToHashSet();
        var accountIdsNullable = accounts.Select(a => (Guid?)a.Id).ToList();

        // Araliqdaki tamamlanmis emeliyyatlar
        var transactions = await _context.Transactions
            .Where(t => t.Status == TransactionStatus.Completed &&
                        t.CreatedAt >= start && t.CreatedAt <= end &&
                        (accountIdsNullable.Contains(t.FromAccountId) ||
                         accountIdsNullable.Contains(t.ToAccountId)))
            .ToListAsync();

        // Umumi gelir / xerc
        decimal income = 0;
        decimal expense = 0;
        foreach (var t in transactions)
        {
            if (t.ToAccountId.HasValue && accountIds.Contains(t.ToAccountId.Value))
                income += t.Amount;
            if (t.FromAccountId.HasValue && accountIds.Contains(t.FromAccountId.Value))
                expense += t.Amount;
        }

        // Gunluk bolgu
        var daily = transactions
            .GroupBy(t => t.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailySummaryDto
            {
                Date = g.Key,
                Income = g.Where(t => t.ToAccountId.HasValue && accountIds.Contains(t.ToAccountId.Value))
                          .Sum(t => t.Amount),
                Expense = g.Where(t => t.FromAccountId.HasValue && accountIds.Contains(t.FromAccountId.Value))
                           .Sum(t => t.Amount)
            })
            .ToList();

        return new DateRangeSummaryDto
        {
            StartDate = start,
            EndDate = end,
            TotalIncome = income,
            TotalExpense = expense,
            NetFlow = income - expense,
            DailyBreakdown = daily
        };
    }

    // emeliyyati kateqoriyaya ayir (Description + Type-a gore)
    private static string Categorize(Transaction t)
    {
        var desc = t.Description ?? "";

        if (desc.StartsWith("Kommunal"))
            return "Kommunal";
        if (desc == "Round-up yigim")
            return "Round-up yigim";
        if (desc.Contains("Kredit") || desc.Contains("kredit"))
            return "Kredit odenisi";

        return t.Type switch
        {
            TransactionType.Withdraw => "Nagd cixaris",
            TransactionType.Transfer => "Kocurme",
            _ => "Diger"
        };
    }

    // kateqoriya uzre xerc hesabati (secilen ay)
    public async Task<SpendingReportDto> GetSpendingByCategoryAsync(string userId, int? year, int? month)
    {
        var now = DateTime.UtcNow;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        var monthStart = DateTime.SpecifyKind(new DateTime(y, m, 1), DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        // istifadecinin hesablari
        var myAccountIds = await _context.Accounts
            .Where(a => a.OwnerId == userId)
            .Select(a => a.Id)
            .ToListAsync();

        // bu ayin CIXAN emeliyyatlari (xercler)
        var spent = await _context.Transactions
            .Where(t => t.FromAccountId != null
                     && myAccountIds.Contains(t.FromAccountId.Value)
                     && t.Status == TransactionStatus.Completed
                     && t.CreatedAt >= monthStart
                     && t.CreatedAt < monthEnd)
            .ToListAsync();

        var total = spent.Sum(t => t.Amount);

        // kateqoriyalara bol
        var categories = spent
            .GroupBy(Categorize)
            .Select(g => new CategorySpendingDto
            {
                Category = g.Key,
                Amount = g.Sum(t => t.Amount),
                Count = g.Count(),
                Percentage = total > 0 ? Math.Round(g.Sum(t => t.Amount) / total * 100, 1) : 0
            })
            .OrderByDescending(c => c.Amount)
            .ToList();

        return new SpendingReportDto
        {
            Year = y,
            Month = m,
            TotalSpent = total,
            Categories = categories
        };
    }

    // son N ayin xerc trendi (ay-ay muqayise)
    public async Task<List<MonthlySpendingDto>> GetMonthlySpendingAsync(string userId, int months)
    {
        if (months < 1) months = 6;
        if (months > 24) months = 24;

        var myAccountIds = await _context.Accounts
            .Where(a => a.OwnerId == userId)
            .Select(a => a.Id)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var start = DateTime.SpecifyKind(
            new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1)), DateTimeKind.Utc);

        var spent = await _context.Transactions
            .Where(t => t.FromAccountId != null
                     && myAccountIds.Contains(t.FromAccountId.Value)
                     && t.Status == TransactionStatus.Completed
                     && t.CreatedAt >= start)
            .ToListAsync();

        // ay-ay qrupla
        var result = new List<MonthlySpendingDto>();
        for (int i = 0; i < months; i++)
        {
            var monthStart = start.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1);

            result.Add(new MonthlySpendingDto
            {
                Year = monthStart.Year,
                Month = monthStart.Month,
                TotalSpent = spent
                    .Where(t => t.CreatedAt >= monthStart && t.CreatedAt < monthEnd)
                    .Sum(t => t.Amount)
            });
        }

        return result;
    }
}