using BankApiApp.DTOs.Loan;

namespace BankApiApp.Services;

public interface ILoanService
{
    Task<LoanDto> RequestLoanAsync(string userId, LoanRequestDto dto);
    Task<List<LoanDto>> GetMyLoansAsync(string userId);
    Task<List<LoanDto>> GetPendingLoansAsync();
    Task<LoanDto> ApproveLoanAsync(Guid loanId);
    Task<LoanDto> RejectLoanAsync(Guid loanId);
    Task ProcessDuePaymentsAsync();
}