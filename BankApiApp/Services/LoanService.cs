using AutoMapper;
using BankApiApp.Data;
using BankApiApp.DTOs.Loan;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class LoanService : ILoanService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private const decimal AnnualInterestRate = 12m;  // bankın illik faizi (%)

    public LoanService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<LoanDto> RequestLoanAsync(string userId, LoanRequestDto dto)
    {
        if (dto.Principal <= 0)
            throw new Exception("Kredit məbləği 0-dan böyük olmalıdır.");
        if (dto.TermMonths <= 0)
            throw new Exception("Müddət 0-dan böyük olmalıdır.");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId && a.OwnerId == userId);
        if (account == null)
            throw new Exception("Hesab tapılmadı.");

        // AMORTIZASIYA: aylıq ödəniş və ümumi məbləği hesabla
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
            Status = LoanStatus.Pending      // hələ admin təsdiqi gözləyir
        };

        _context.Loans.Add(loan);
        await _context.SaveChangesAsync();

        return _mapper.Map<LoanDto>(loan);
    }

    public async Task<List<LoanDto>> GetMyLoansAsync(string userId)
    {
        var loans = await _context.Loans
            .Include(l => l.Payments)
            .Where(l => l.Account.OwnerId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return _mapper.Map<List<LoanDto>>(loans);
    }

    // Amortizasiya hesablaması
    private (decimal monthlyPayment, decimal totalPayable) CalculateAmortization(
        decimal principal, decimal annualRate, int termMonths)
    {
        var rate = annualRate / 100m;          // 12% → 0.12
        var years = termMonths / 12m;          // 12 ay → 1 il
        var interest = principal * rate * years; // faiz məbləği
        var total = principal + interest;       // əsas + faiz
        var monthly = Math.Round(total / termMonths, 2); // aylıq ödəniş
        total = monthly * termMonths;           // yuvarlaqlaşdırmadan sonra tutarlı saxla
        return (monthly, total);
    }
    public async Task<List<LoanDto>> GetPendingLoansAsync()
    {
        var loans = await _context.Loans
            .Include(l => l.Payments)
            .Where(l => l.Status == LoanStatus.Pending)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();
        return _mapper.Map<List<LoanDto>>(loans);
    }

    public async Task<LoanDto> ApproveLoanAsync(Guid loanId)
    {
        var loan = await _context.Loans
            .Include(l => l.Account)
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan == null)
            throw new Exception("Kredit tapılmadı.");
        if (loan.Status != LoanStatus.Pending)
            throw new Exception("Yalnız gözləyən kredit təsdiqlənə bilər.");

        await using var dbTx = await _context.Database.BeginTransactionAsync();
        try
        {
            loan.Status = LoanStatus.Active;
            loan.StartDate = DateTime.UtcNow;

            // 1. krediti hesaba köçür (istifadəçi pulu alır)
            loan.Account.Balance += loan.Principal;
            _context.Transactions.Add(new Transaction
            {
                Amount = loan.Principal,
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed,
                ToAccountId = loan.AccountId,
                Description = "Kredit verildi"
            });

            // 2. ödəniş cədvəlini yarat (TermMonths sətir)
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
            throw new Exception("Kredit tapılmadı.");
        if (loan.Status != LoanStatus.Pending)
            throw new Exception("Yalnız gözləyən kredit rədd edilə bilər.");

        loan.Status = LoanStatus.Rejected;
        await _context.SaveChangesAsync();
        return _mapper.Map<LoanDto>(loan);
    }
}