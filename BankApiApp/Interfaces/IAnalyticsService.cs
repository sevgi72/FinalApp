using BankApiApp.DTOs.Analytics;

namespace BankApiApp.Services;

public interface IAnalyticsService
{
    Task<DashboardDto> GetDashboardAsync(string userId);
    Task<DateRangeSummaryDto> GetSummaryAsync(string userId, DateTime? startDate, DateTime? endDate);
    Task<SpendingReportDto> GetSpendingByCategoryAsync(string userId, int? year, int? month);
    Task<List<MonthlySpendingDto>> GetMonthlySpendingAsync(string userId, int months);
}