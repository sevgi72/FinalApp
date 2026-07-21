using BankApiApp.DTOs.Cashback;

namespace BankApiApp.Services;

public interface ICashbackService
{
    // emeliyyatdan sonra cagirilir - kesbek hesablayir ve elave edir
    // accountId: kesbek hansi hesaba aiddir (status hesablamasi ucun lazimdir)
    Task TryEarnAsync(string userId, Guid accountId, decimal spentAmount, string category);

    Task<CashbackStatusDto> GetStatusAsync(string userId);

    // bonus balansi esas hesaba kocur
    Task<string> RedeemAsync(string userId, Guid targetAccountId);
}