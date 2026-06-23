using BankApiApp.DTOs.Payment;
using BankApiApp.DTOs.Payments;

namespace BankApiApp.Services;

public interface IPaymentService
{
    Task<PaymentResultDto> TopUpAsync(string userId, TopUpDto dto);
    Task<List<PaymentHistoryDto>> GetMyPaymentsAsync(string userId);
}