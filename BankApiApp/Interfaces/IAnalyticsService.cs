using BankApiApp.DTOs.Analytics;

namespace BankApiApp.Services;

public interface IAnalyticsService
{
    Task<DashboardDto> GetDashboardAsync(string userId);
    Task<DateRangeSummaryDto> GetSummaryAsync(string userId, DateTime? startDate, DateTime? endDate);
}