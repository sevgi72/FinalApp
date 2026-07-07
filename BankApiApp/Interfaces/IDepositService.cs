using BankApiApp.DTOs.Deposit;

namespace BankApiApp.Services;

public interface IDepositService
{
    Task<DepositDto> OpenAsync(string userId, CreateDepositDto dto);
    Task<List<DepositDto>> GetMyAsync(string userId);
    Task<string> CloseAsync(string userId, Guid depositId);

    // Hangfire her ay faiz hesablayir
    Task AccrueInterestAsync();
}