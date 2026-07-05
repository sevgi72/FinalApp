using AutoMapper;
using BankApiApp.Data;
using BankApiApp.DTOs.Common;
using BankApiApp.DTOs.Loan;
using BankApiApp.Exceptions;
using BankApiApp.Extensions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class LoanService : ILoanService
{
    private readonly ILocalizationService _localizer;
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private const decimal AnnualInterestRate = 12m;  // bankin illik faizi (%)

    public LoanService(AppDbContext context, IMapper mapper, ILocalizationService localizer)
    {
        _localizer = localizer;
        _context = context;
        _mapper = mapper;
    }

    public async Task<LoanDto> RequestLoanAsync(string userId, LoanRequestDto dto)
    {
        if (dto.Principal <= 0)
            throw new BadRequestException(_localizer.Get("AmountMustBePositive"));
        if (dto.TermMonths <= 0)
            throw new BadRequestException(_localizer.Get("DurationMustBePositive"));

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId && a.OwnerId == userId);
        if (account == null)
            throw new NotFoundException(_localizer.Get("AccountNotFound"));

        // AMORTIZASIYA: ayliq odenis ve umumi meblegi hesabla
        var (monthlyPayment, totalPayable) =
            CalculateAmortization(dto.Principal, AnnualInterestRate, dto.TermMonths);

        var loan = new Loan
        {
            AccountId = account.Id,
            Principal = dto.Principal,
            InterestRate = AnnualInterestRate,
            TermMonths = dto.TermMonths,
            MonthlyPayment = monthlyPayment,
            TotalPayable = totalPayable,
            RemainingBalance = totalPayable,
            Status = LoanStatus.Pending      // hele admin tesdiqi gozleyir
        };

        _context.Loans.Add(loan);
        await _context.SaveChangesAsync();

        return _mapper.Map<LoanDto>(loan);
    }

    // Amortizasiya hesablamasi
    private (decimal monthlyPayment, decimal totalPayable) CalculateAmortization(
        decimal principal, decimal annualRate, int termMonths)
    {
        var rate = annualRate / 100m;          // 12% → 0.12
        var years = termMonths / 12m;          // 12 ay → 1 il
        var interest = principal * rate * years; // faiz meblegi
        var total = principal + interest;       // esas + faiz
        var monthly = Math.Round(total / termMonths, 2); // ayliq odenis
        total = monthly * termMonths;           // yuvarlaqlasdirmadan sonra tutarli saxla
        return (monthly, total);
    }

    public async Task<PagedResult<LoanDto>> GetMyLoansAsync(
    string userId, PaginationParams pagination)
    {
        var query = _context.Loans
            .Include(l => l.Payments)
            .Where(l => l.Account.OwnerId == userId)
            .OrderByDescending(l => l.CreatedAt);

        var paged = await query.ToPagedResultAsync(pagination);

        return new PagedResult<LoanDto>
        {
            Items = _mapper.Map<List<LoanDto>>(paged.Items),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages
        };
    }

    public async Task<PagedResult<LoanDto>> GetPendingLoansAsync(PaginationParams pagination)
    {
        var query = _context.Loans
            .Include(l => l.Payments)
            .Where(l => l.Status == LoanStatus.Pending)
            .OrderBy(l => l.CreatedAt);

        var paged = await query.ToPagedResultAsync(pagination);

        return new PagedResult<LoanDto>
        {
            Items = _mapper.Map<List<LoanDto>>(paged.Items),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages
        };
    }

    public async Task<LoanDto> ApproveLoanAsync(Guid loanId)
    {
        var loan = await _context.Loans
            .Include(l => l.Account)
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan == null)
            throw new NotFoundException(_localizer.Get("LoanNotFound"));
        if (loan.Status != LoanStatus.Pending)
            throw new BadRequestException(_localizer.Get("OnlyPendingLoanCanBeApproved"));

        await using var dbTx = await _context.Database.BeginTransactionAsync();
        try
        {
            loan.Status = LoanStatus.Active;
            loan.StartDate = DateTime.UtcNow;

            // 1. krediti hesaba kocur (istifadeci pulu alir)
            loan.Account.Balance += loan.Principal;
            _context.Transactions.Add(new Transaction
            {
                Amount = loan.Principal,
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed,
                ToAccountId = loan.AccountId,
                Description = "Kredit verildi"
            });

            // 2. odenis cedvelini yarat (TermMonths setir)
            for (int i = 1; i <= loan.TermMonths; i++)
            {
                _context.LoanPayments.Add(new LoanPayment
                {
                    LoanId = loan.Id,
                    PaymentNumber = i,
                    DueDate = loan.StartDate.Value.AddMonths(i),
                    Amount = loan.MonthlyPayment,
                    IsPaid = false
                });
            }

            await _context.SaveChangesAsync();
            await dbTx.CommitAsync();
        }
        catch
        {
            await dbTx.RollbackAsync();
            throw;
        }

        var updated = await _context.Loans.Include(l => l.Payments).FirstAsync(l => l.Id == loanId);
        return _mapper.Map<LoanDto>(updated);
    }

    public async Task<LoanDto> RejectLoanAsync(Guid loanId)
    {
        var loan = await _context.Loans.FirstOrDefaultAsync(l => l.Id == loanId);
        if (loan == null)
            throw new NotFoundException(_localizer.Get("LoanNotFound"));
        if (loan.Status != LoanStatus.Pending)
            throw new BadRequestException(_localizer.Get("OnlyPendingLoanCanBeRejected"));

        loan.Status = LoanStatus.Rejected;
        await _context.SaveChangesAsync();
        return _mapper.Map<LoanDto>(loan);
    }

    public async Task ProcessDuePaymentsAsync()
    {
        var now = DateTime.UtcNow;

        // vaxti catmis, odenilmemis odenisler (aktiv kreditler)
        var duePayments = await _context.LoanPayments
            .Include(p => p.Loan)
                .ThenInclude(l => l.Account)
            .Where(p => !p.IsPaid
                        && p.DueDate <= now
                        && p.Loan.Status == LoanStatus.Active)
            .OrderBy(p => p.DueDate)
            .ToListAsync();

        foreach (var payment in duePayments)
        {
            var loan = payment.Loan;
            var account = loan.Account;

            // balans kifayet etmirse bu defe kec (novbeti dovrde yeniden cehd)
            if (account.Balance < payment.Amount)
                continue;

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                account.Balance -= payment.Amount;
                payment.IsPaid = true;
                payment.PaidAt = now;
                loan.RemainingBalance -= payment.Amount;

                _context.Transactions.Add(new Transaction
                {
                    Amount = payment.Amount,
                    Type = TransactionType.Withdraw,
                    Status = TransactionStatus.Completed,
                    FromAccountId = account.Id,
                    Description = $"Kredit odenisi #{payment.PaymentNumber}"
                });

                // butun borc bitibse kredit baglanir
                if (loan.RemainingBalance <= 0)
                    loan.Status = LoanStatus.Completed;

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
            }
        }
    }
}