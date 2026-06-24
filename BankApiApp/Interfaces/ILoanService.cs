using BankApiApp.DTOs.Common;
using BankApiApp.DTOs.Loan;

namespace BankApiApp.Services;

public interface ILoanService
{
    Task<LoanDto> RequestLoanAsync(string userId, LoanRequestDto dto);
    Task<PagedResult<LoanDto>> GetMyLoansAsync(string userId, PaginationParams pagination);
    Task<PagedResult<LoanDto>> GetPendingLoansAsync(PaginationParams pagination);
    Task<LoanDto> ApproveLoanAsync(Guid loanId);
    Task<LoanDto> RejectLoanAsync(Guid loanId);
    Task ProcessDuePaymentsAsync();
}