using BankApiApp.DTOs.Common;
using BankApiApp.DTOs.Utility;

namespace BankApiApp.Services;

public interface IUtilityService
{
    Task<List<UtilityProviderDto>> GetProvidersAsync();
    Task<DebtResponseDto> GetDebtAsync(DebtInquiryDto dto);
    Task<UtilityPaymentDto> PayAsync(string userId, PayUtilityDto dto);
    Task<PagedResult<UtilityPaymentDto>> GetMyPaymentsAsync(string userId, PaginationParams pagination);
    Task<(byte[] Pdf, string FileName)> GetReceiptPdfAsync(string userId, Guid paymentId);
}