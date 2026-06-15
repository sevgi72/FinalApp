using BankApiApp.DTOs.Account;

namespace BankApiApp.Services;

public interface IAccountService
{
    Task<AccountDto> CreateAccountAsync(string userId, CreateAccountDto dto);
    Task<List<AccountDto>> GetMyAccountsAsync(string userId);
    Task<AccountDto> GetByIdAsync(string userId, Guid accountId);
}