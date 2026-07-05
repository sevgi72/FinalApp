using BankApiApp.DTOs.RoundUp;
using BankApiApp.Models;

namespace BankApiApp.Services;

public interface IRoundUpService
{
    Task<string> EnableAsync(string userId, EnableRoundUpDto dto);
    Task<string> DisableAsync(string userId);
    Task<RoundUpStatusDto> GetStatusAsync(string userId);
    Task TryProcessAsync(Account sourceAccount, decimal spentAmount); // Tranzaksiyaların çağırdığı metod
}