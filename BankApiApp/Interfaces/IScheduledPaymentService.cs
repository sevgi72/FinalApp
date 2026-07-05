using BankApiApp.DTOs.Utility;

namespace BankApiApp.Services;

public interface IScheduledPaymentService
{
    Task<ScheduledPaymentDto> CreateAsync(string userId, CreateScheduledPaymentDto dto);
    Task<List<ScheduledPaymentDto>> GetMyAsync(string userId);
    Task<string> ToggleAsync(string userId, Guid id, bool isActive);
    Task<string> DeleteAsync(string userId, Guid id);

    // Hangfire her gun cagirir
    Task ProcessDuePaymentsAsync();
}