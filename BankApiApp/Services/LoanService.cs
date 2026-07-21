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
    private const decimal AnnualInterestRate = 12m; // Bankın illik faizi (%)

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

        // AMORTİZASİYA: Aylıq ödəniş və ümumi məbləğ
        var (monthlyPayment, totalPayable) = CalculateAmortization(dto.Principal, AnnualInterestRate, dto.TermMonths);

        var loan = new Loan
        {
            AccountId = account.Id,
            Principal = dto.Principal,
            InterestRate = AnnualInterestRate,
            TermMonths = dto.TermMonths,
            MonthlyPayment = monthlyPayment,
            TotalPayable = totalPayable,
            RemainingBalance = totalPayable,
            Status = LoanStatus.Pending // Əvvəlcə admin təsdiqi gözləyir
        };

        _context.Loans.Add(loan);
        await _context.SaveChangesAsync();

        return _mapper.Map<LoanDto>(loan);
    }

    // Amortizasiya hesablaması (Dəqiq Maliyyə Hesablanması)
    private (decimal monthlyPayment, decimal totalPayable) CalculateAmortization(
        decimal principal, decimal annualRate, int termMonths)
    {
        var rate = annualRate / 100m;
        var years = termMonths / 12m;
        var interest = principal * rate * years;
        var total = Math.Round(principal + interest, 2, MidpointRounding.AwayFromZero);
        var monthly = Math.Round(total / termMonths, 2, MidpointRounding.AwayFromZero);
        
        return (monthly, total);
    }

    public async Task<PagedResult<LoanDto>> GetMyLoansAsync(string userId, PaginationParams pagination)
    {
        var query = _context.Loans
            .AsNoTracking() // ⚡ Məhsuldarlıq üçün AsNoTracking
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
            .AsNoTracking() // ⚡ Məhsuldarlıq üçün AsNoTracking
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

            // 1. Krediti hesaba köçür (Məbləğ istifadəçi balansına oturur)
            loan.Account.Balance += loan.Principal;
            
            _context.Transactions.Add(new Transaction
            {
                Amount = loan.Principal,
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed,
                ToAccountId = loan.AccountId,
                Description = $"Kredit təsdiqləndi: {loan.Principal:N2} AZN"
            });

            // 2. Ödəniş cədvəlini yarat (Qalıq qəpiklər sonuncu aya düzəliş edilir)
            decimal accumulated = 0m;
            for (int i = 1; i <= loan.TermMonths; i++)
            {
                decimal currentPaymentAmount;
                if (i == loan.TermMonths)
                {
                    // Sonuncu ay: Qalıq uyğunsuzluq olmasın deyə dəqiq fərq götürülür
                    currentPaymentAmount = loan.TotalPayable - accumulated;
                }
                else
                {
                    currentPaymentAmount = loan.MonthlyPayment;
                    accumulated += currentPaymentAmount;
                }

                _context.LoanPayments.Add(new LoanPayment
                {
                    LoanId = loan.Id,
                    PaymentNumber = i,
                    DueDate = loan.StartDate.Value.AddMonths(i),
                    Amount = currentPaymentAmount,
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

        var updated = await _context.Loans
            .AsNoTracking()
            .Include(l => l.Payments)
            .FirstAsync(l => l.Id == loanId);

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

    // Hangfire Cron Job üçün optimizasiya olunmuş avtomatik tutulma metodu
    public async Task ProcessDuePaymentsAsync()
    {
        var now = DateTime.UtcNow;

        // Vaxtı çatmış və ödənilməmiş ödənişlər (Aktiv kreditlər)
        var duePayments = await _context.LoanPayments
            .Include(p => p.Loan)
                .ThenInclude(l => l.Account)
            .Where(p => !p.IsPaid
                        && p.DueDate <= now
                        && p.Loan.Status == LoanStatus.Active)
            .OrderBy(p => p.DueDate)
            .ToListAsync();

        if (!duePayments.Any()) return;

        foreach (var payment in duePayments)
        {
            var loan = payment.Loan;
            var account = loan.Account;

            // Balans kifayət etmirsə bu ödənişi keç (Növbəti gün yenidən cəhd olunacaq)
            if (account.Balance < payment.Amount)
                continue;

            try
            {
                // Məbləğlər yenilənir
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
                    Description = $"Avtomatik kredit ödənişi #{payment.PaymentNumber}"
                });

                // Əgər qalıq borc tam sıfırlanıbsa kredit bağlanır
                if (loan.RemainingBalance <= 0.01m)
                {
                    loan.RemainingBalance = 0m;
                    loan.Status = LoanStatus.Completed;
                }
            }
            catch
            {
                // Fərdi xətalar ümumi dövrəni qırmır
                continue;
            }
        }

        // Bütün uğurlu tutulmaları tək bir verilənlər bazası sorğusunda təsdiqləyirik
        await _context.SaveChangesAsync();
    }
}