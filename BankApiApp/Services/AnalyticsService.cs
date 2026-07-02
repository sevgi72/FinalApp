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
}