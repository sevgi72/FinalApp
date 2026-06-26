using BankApiApp.DTOs.Limits;
using BankApiApp.Models;

namespace BankApiApp.Services;

public interface ILimitService
{
    Task EnsureWithinLimitsAsync(Account account, decimal amount);   // köçürmə öncəsi yoxlama
    Task<LimitInfoDto> GetLimitInfoAsync(string userId, Guid accountId);
    Task<LimitInfoDto> UpdateLimitsAsync(string userId, Guid accountId, UpdateLimitsDto dto);
}