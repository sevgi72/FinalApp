using BankApiApp.DTOs.CashByCode;

namespace BankApiApp.Services;

public interface ICashByCodeService
{
    Task<CashByCodeDto> CreateAsync(string userId, CreateCashByCodeDto dto);
    Task<string> ClaimAsync(string userId, ClaimCashByCodeDto dto);
    Task<string> CancelAsync(string userId, Guid id);
    Task<List<CashByCodeDto>> GetMyAsync(string userId);

    // Hangfire: vaxti kecenleri qaytar
    Task ExpireOldCodesAsync();
}