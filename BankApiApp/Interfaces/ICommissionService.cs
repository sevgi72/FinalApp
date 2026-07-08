using BankApiApp.DTOs.Commission;

namespace BankApiApp.Services;

public interface ICommissionService
{
    // komissiya hesabla (emeliyyat vaxti cagirilir)
    Task<decimal> CalculateAsync(string transactionType, decimal amount);

    // admin - qaydalari gor / yenile
    Task<List<CommissionRuleDto>> GetRulesAsync();
    Task<string> UpdateRuleAsync(Guid id, UpdateCommissionRuleDto dto);
}