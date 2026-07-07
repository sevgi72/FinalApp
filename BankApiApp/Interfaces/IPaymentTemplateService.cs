using BankApiApp.DTOs.Utility;

namespace BankApiApp.Services;

public interface IPaymentTemplateService
{
    Task<PaymentTemplateDto> CreateAsync(string userId, CreatePaymentTemplateDto dto);
    Task<List<PaymentTemplateDto>> GetMyAsync(string userId);
    Task<string> DeleteAsync(string userId, Guid id);
}