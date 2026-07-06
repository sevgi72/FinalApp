using BankApiApp.Data;
using BankApiApp.DTOs.CashByCode;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class CashByCodeService : ICashByCodeService
{
    private readonly AppDbContext _context;
    private readonly ILimitService _limitService;
    private readonly ILocalizationService _localizer;
    private readonly ILogger<CashByCodeService> _logger;

    private const int ExpiryHours = 72;

    public CashByCodeService(AppDbContext context, ILimitService limitService,
        ILocalizationService localizer, ILogger<CashByCodeService> logger)
    {
        _context = context;
        _limitService = limitService;
        _localizer = localizer;
        _logger = logger;
    }

    // kod yarat (pul rezerv olunur)
    public async Task<CashByCodeDto> CreateAsync(string userId, CreateCashByCodeDto dto)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        if (account.Status != AccountStatus.Active)
            throw new BadRequestException(_localizer.Get("AccountNotActive"));

        if (account.Balance < dto.Amount)
            throw new BadRequestException(_localizer.Get("InsufficientBalance"));

        // limit yoxlamasi (pul cixir)
        await _limitService.EnsureWithinLimitsAsync(account, dto.Amount);

        await using var trx = await _context.Database.BeginTransactionAsync();

        // pul derhal cixir (rezerv)
        account.Balance -= dto.Amount;

        var cash = new CashByCode
        {
            Code = await GenerateUniqueCodeAsync(),
            Amount = dto.Amount,
            SenderUserId = userId,
            SenderAccountId = account.Id,
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddHours(ExpiryHours)
        };
        _context.CashByCodes.Add(cash);

        _context.Transactions.Add(new Transaction
        {
            Amount = dto.Amount,
            Type = TransactionType.Withdraw,
            Status = TransactionStatus.Completed,
            FromAccountId = account.Id,
            Description = $"Kodla kocurme yaradildi ({cash.Code})"
        });

        await _context.SaveChangesAsync();
        await trx.CommitAsync();

        return new CashByCodeDto
        {
            Id = cash.Id,
            Code = cash.Code,
            Amount = cash.Amount,
            Status = cash.Status,
            ExpiresAt = cash.ExpiresAt,
            CreatedAt = cash.CreatedAt
        };
    }

    // kodla pulu gotur
    public async Task<string> ClaimAsync(string userId, ClaimCashByCodeDto dto)
    {
        var cash = await _context.CashByCodes
            .FirstOrDefaultAsync(c => c.Code == dto.Code && c.Status == "Pending");

        // kod yoxdur / artiq istifade olunub / vaxti kecib → eyni mesaj (sizdirma olmasin)
        if (cash == null || cash.ExpiresAt < DateTime.UtcNow)
            throw new BadRequestException(_localizer.Get("InvalidOrExpiredCode"));

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        if (account.Status != AccountStatus.Active)
            throw new BadRequestException(_localizer.Get("AccountNotActive"));

        await using var trx = await _context.Database.BeginTransactionAsync();

        account.Balance += cash.Amount;
        cash.Status = "Claimed";
        cash.ClaimedByUserId = userId;
        cash.ClaimedAt = DateTime.UtcNow;

        _context.Transactions.Add(new Transaction
        {
            Amount = cash.Amount,
            Type = TransactionType.Deposit,
            Status = TransactionStatus.Completed,
            ToAccountId = account.Id,
            Description = $"Kodla kocurme goturuldu ({cash.Code})"
        });

        await _context.SaveChangesAsync();
        await trx.CommitAsync();

        return $"{cash.Amount:0.00} AZN hesabiniza kocuruldu.";
    }

    // gonderilen legv edir (pul qayidir)
    public async Task<string> CancelAsync(string userId, Guid id)
    {
        var cash = await _context.CashByCodes
            .FirstOrDefaultAsync(c => c.Id == id && c.SenderUserId == userId)
            ?? throw new NotFoundException(_localizer.Get("PaymentNotFound"));

        if (cash.Status != "Pending")
            throw new BadRequestException(_localizer.Get("InvalidOrExpiredCode"));

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == cash.SenderAccountId);

        await using var trx = await _context.Database.BeginTransactionAsync();

        cash.Status = "Cancelled";
        if (account != null)
        {
            account.Balance += cash.Amount;
            _context.Transactions.Add(new Transaction
            {
                Amount = cash.Amount,
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed,
                ToAccountId = account.Id,
                Description = $"Kodla kocurme legv olundu ({cash.Code})"
            });
        }

        await _context.SaveChangesAsync();
        await trx.CommitAsync();

        return "Kod legv olundu, mebleg hesabiniza qaytarildi.";
    }

    // menim yaratdigim kodlar
    public async Task<List<CashByCodeDto>> GetMyAsync(string userId)
    {
        return await _context.CashByCodes
            .Where(c => c.SenderUserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CashByCodeDto
            {
                Id = c.Id,
                Code = c.Code,
                Amount = c.Amount,
                Status = c.Status,
                ExpiresAt = c.ExpiresAt,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
    }

    // HANGFIRE: vaxti kecmis kodlari qaytar
    public async Task ExpireOldCodesAsync()
    {
        var expired = await _context.CashByCodes
            .Where(c => c.Status == "Pending" && c.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        if (!expired.Any())
        {
            _logger.LogInformation("Kodla kocurme: vaxti kecen kod yoxdur.");
            return;
        }

        foreach (var cash in expired)
        {
            try
            {
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == cash.SenderAccountId);

                cash.Status = "Expired";
                if (account != null)
                {
                    account.Balance += cash.Amount;
                    _context.Transactions.Add(new Transaction
                    {
                        Amount = cash.Amount,
                        Type = TransactionType.Deposit,
                        Status = TransactionStatus.Completed,
                        ToAccountId = account.Id,
                        Description = $"Kodla kocurme vaxti bitdi, qaytarildi ({cash.Code})"
                    });
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Vaxti kecen kod qaytarildi: {Code}", cash.Code);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kod qaytarma xetasi: {Id}", cash.Id);
            }
        }
    }

    // unikal 8 reqemli kod
    private async Task<string> GenerateUniqueCodeAsync()
    {
        string code;
        do
        {
            code = Random.Shared.Next(10000000, 99999999).ToString();
        }
        while (await _context.CashByCodes.AnyAsync(c => c.Code == code && c.Status == "Pending"));

        return code;
    }
}