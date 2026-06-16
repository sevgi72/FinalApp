using BankApiApp.DTOs.Transaction;

namespace BankApiApp.Services;

public interface ITransactionService
{
    Task<TransactionDto> DepositAsync(string userId, DepositDto dto);
    Task<TransactionDto> WithdrawAsync(string userId, WithdrawDto dto);
    Task<TransactionDto> TransferAsync(string userId, TransferDto dto);
}