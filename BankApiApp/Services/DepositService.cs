using BankApiApp.Data;
using BankApiApp.DTOs.Deposit;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class DepositService : IDepositService
{
    private readonly AppDbContext _context;
    private readonly ILocalizationService _localizer;
    private readonly ILogger<DepositService> _logger;

    // butun emanetler ucun standart illik faiz (12%)
    private const decimal DefaultAnnualRate = 0.12m;
    private const decimal MinAmount = 10m;

    public DepositService(AppDbContext context,
        ILocalizationService localizer,
        ILogger<DepositService> logger)
    {
        _context = context;
        _localizer = localizer;
        _logger = logger;
    }

    // emanet ac (esas hesabdan pul cixir, xezineye kocur)
    public async Task<DepositDto> OpenAsync(string userId, CreateDepositDto dto)
    {
        if (dto.Amount < MinAmount)
            throw new BadRequestException($"Minimum emanet {MinAmount} AZN-dir.");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.SourceAccountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        if (account.Status != AccountStatus.Active)
            throw new BadRequestException(_localizer.Get("AccountNotActive"));

        if (account.Balance < dto.Amount)
            throw new BadRequestException(_localizer.Get("InsufficientBalance"));

        await using var trx = await _context.Database.BeginTransactionAsync();

        // pul esas hesabdan cixir
        account.Balance -= dto.Amount;

        var deposit = new Deposit
        {
            UserId = userId,
            SourceAccountId = account.Id,
            Principal = dto.Amount,
            EarnedInterest = 0,
            AnnualRate = DefaultAnnualRate,
            IsActive = true,
            LastInterestAt = DateTime.UtcNow
        };
        _context.Deposits.Add(deposit);

        // emeliyyat tarixcesi
        _context.Transactions.Add(new Transaction
        {
            Amount = dto.Amount,
            Type = TransactionType.Withdraw,
            Status = TransactionStatus.Completed,
            FromAccountId = account.Id,
            Description = "Emanet acildi"
        });

        await _context.SaveChangesAsync();
        await trx.CommitAsync();

        return MapToDto(deposit);
    }

    public async Task<List<DepositDto>> GetMyAsync(string userId)
    {
        var deposits = await _context.Deposits
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return deposits.Select(MapToDto).ToList();
    }

    // emaneti bagla (principal + faiz esas hesaba qayidir)
    public async Task<string> CloseAsync(string userId, Guid depositId)
    {
        var deposit = await _context.Deposits
            .FirstOrDefaultAsync(d => d.Id == depositId && d.UserId == userId)
            ?? throw new NotFoundException("Emanet tapilmadi.");

        if (!deposit.IsActive)
            throw new BadRequestException("Emanet artiq baglanib.");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == deposit.SourceAccountId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        var total = deposit.Principal + deposit.EarnedInterest;

        await using var trx = await _context.Database.BeginTransactionAsync();

        account.Balance += total;
        deposit.IsActive = false;

        _context.Transactions.Add(new Transaction
        {
            Amount = total,
            Type = TransactionType.Deposit,
            Status = TransactionStatus.Completed,
            ToAccountId = account.Id,
            Description = "Emanet baglandi (esas + faiz)"
        });

        await _context.SaveChangesAsync();
        await trx.CommitAsync();

        return $"{total:0.00} AZN (esas {deposit.Principal:0.00} + faiz {deposit.EarnedInterest:0.00}) hesabiniza qaytarildi.";
    }

    // HANGFIRE: her ay aktiv emanetlere faiz elave et
    public async Task AccrueInterestAsync()
    {
        var activeDeposits = await _context.Deposits
            .Where(d => d.IsActive)
            .ToListAsync();

        if (!activeDeposits.Any())
        {
            _logger.LogInformation("Emanet faizi: aktiv emanet yoxdur.");
            return;
        }

        foreach (var deposit in activeDeposits)
        {
            // aylik faiz = illik / 12
            var monthlyRate = deposit.AnnualRate / 12m;
            var interest = Math.Round(deposit.Principal * monthlyRate, 2);

            deposit.EarnedInterest += interest;
            deposit.LastInterestAt = DateTime.UtcNow;

            _logger.LogInformation("Emanet faizi elave olundu: {Id} +{Interest} AZN", deposit.Id, interest);
        }

        await _context.SaveChangesAsync();
    }

    private static DepositDto MapToDto(Deposit d) => new()
    {
        Id = d.Id,
        Principal = d.Principal,
        EarnedInterest = d.EarnedInterest,
        TotalValue = d.Principal + d.EarnedInterest,
        AnnualRate = d.AnnualRate,
        IsActive = d.IsActive,
        CreatedAt = d.CreatedAt,
        LastInterestAt = d.LastInterestAt
    };
}