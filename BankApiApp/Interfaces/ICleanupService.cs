namespace BankApiApp.Services;

public interface ICleanupService
{
    Task CleanupUnconfirmedAccountsAsync();
}