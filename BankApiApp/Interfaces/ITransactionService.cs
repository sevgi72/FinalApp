using BankApiApp.DTOs.Transaction;
using BankApiApp.DTOs.Transactions;

namespace BankApiApp.Services;

public interface ITransactionService
{
    Task<TransactionDto> DepositAsync(string userId, DepositDto dto);
    Task<TransactionDto> WithdrawAsync(string userId, WithdrawDto dto);
    Task<TransactionDto> TransferAsync(string userId, TransferDto dto);
    Task<List<TransactionDto>> GetAccountTransactionsAsync(string userId, Guid accountId);
    Task TransferByCardAsync(string userId, TransferByCardDto dto);
    Task<ReceiptDto> GetReceiptAsync(string userId, Guid transactionId);
    Task<(byte[] Pdf, string FileName)> GetReceiptPdfAsync(string userId, Guid transactionId);
}