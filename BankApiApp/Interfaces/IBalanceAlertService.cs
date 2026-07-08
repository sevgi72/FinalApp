using BankApiApp.Models;

namespace BankApiApp.Services;

public interface IBalanceAlertService
{
    // hedd teyin et / legv et
    Task<string> SetThresholdAsync(string userId, Guid accountId, decimal? threshold);

    // emeliyyatdan sonra yoxla - lazimsa xeberdarliq gonder
    Task CheckAndAlertAsync(Account account, decimal balanceBefore);
}