using BankApiApp.Data;
using BankApiApp.DTOs.Common;
using BankApiApp.DTOs.Utility;
using BankApiApp.Exceptions;
using BankApiApp.Extensions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace BankApiApp.Services;

public class UtilityService : IUtilityService
{
    private readonly ILocalizationService _localizer;
    private readonly AppDbContext _context;
    private readonly ILimitService _limitService;
    private readonly IRoundUpService _roundUpService;
    private readonly ICashbackService _cashbackService;
    private readonly IBalanceAlertService _balanceAlertService;

    public UtilityService(AppDbContext context, ILimitService limitService,
        IRoundUpService roundUpService, ILocalizationService localizer,
        ICashbackService cashbackService,
        IBalanceAlertService balanceAlertService)
    {
        _localizer = localizer;
        _context = context;
        _limitService = limitService;
        _roundUpService = roundUpService;
        _cashbackService = cashbackService;
        _balanceAlertService = balanceAlertService;
    }

    public async Task<List<UtilityProviderDto>> GetProvidersAsync()
    {
        // DUZELIS 5: yalniz oxuyuruq, deyisdirmirik -> AsNoTracking
        // EF Core bu obyektleri izlemesin (RAM qenaeti + suret).
        return await _context.UtilityProviders
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Category)
            .Select(p => new UtilityProviderDto
            {
                Id = p.Id,
                Name = p.Name,
                Category = p.Category.ToString()
            })
            .ToListAsync();
    }

    public async Task<DebtResponseDto> GetDebtAsync(DebtInquiryDto dto)
    {
        var provider = await _context.UtilityProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == dto.ProviderId && p.IsActive)
            ?? throw new NotFoundException(_localizer.Get("ProviderNotFound"));

        var debt = CalculateDebt(dto.SubscriberCode);

        return new DebtResponseDto
        {
            ProviderName = provider.Name,
            SubscriberCode = dto.SubscriberCode,
            DebtAmount = debt
        };
    }

    public async Task<UtilityPaymentDto> PayAsync(string userId, PayUtilityDto dto)
    {
        var provider = await _context.UtilityProviders
            .FirstOrDefaultAsync(p => p.Id == dto.ProviderId && p.IsActive)
            ?? throw new NotFoundException(_localizer.Get("ProviderNotFound"));

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        if (account.Status != AccountStatus.Active)
            throw new BadRequestException(_localizer.Get("AccountNotActive"));

        if (account.Balance < dto.Amount)
            throw new BadRequestException(_localizer.Get("InsufficientBalance"));

        await _limitService.EnsureWithinLimitsAsync(account, dto.Amount);

        var balanceBefore = account.Balance;   // azalmadan evvelki balans

        await using var trx = await _context.Database.BeginTransactionAsync();
        try
        {
            account.Balance -= dto.Amount;

            var payment = new UtilityPayment
            {
                ProviderId = provider.Id,
                SubscriberCode = dto.SubscriberCode,
                Amount = dto.Amount,
                AccountId = account.Id,
                Status = "Completed"
            };
            _context.UtilityPayments.Add(payment);

            _context.Transactions.Add(new Transaction
            {
                Amount = dto.Amount,
                Type = TransactionType.Withdraw,
                Status = TransactionStatus.Completed,
                FromAccountId = account.Id,
                FromBalanceSnapshot = account.Balance,   // odenisden sonraki balans
                Description = $"Kommunal odenis: {provider.Name} ({dto.SubscriberCode})"
            });

            // DUZELIS 3 (Secim B): Round-up balansi deyisdiyi ucun
            // transaction DAXILINDE cagirilir. TryProcessAsync oz SaveChanges-ini
            // etmir -- deyisiklikler asagidaki SaveChanges ile BIRLIKDE saxlanir.
            // Beleliklede odenis + round-up atomikdir: ya hamisi, ya hec biri.
            await _roundUpService.TryProcessAsync(account, dto.Amount);

            await _context.SaveChangesAsync();
            await trx.CommitAsync();

            // Commit-den SONRA -- esas emeliyyata tesir etmeyen "side-effect"-ler:

            // Cashback bonusdur, oz SaveChanges-i var (best-effort). Qesden esas
            // odenisden AYRIdir ki, kesbek xetasi odenisi poza bilmesin.
            await _cashbackService.TryEarnAsync(userId, account.Id, dto.Amount, "Kommunal");

            // BalanceAlert sadece bildirisdir -- commit-den sonra gonderilir.
            await _balanceAlertService.CheckAndAlertAsync(account, balanceBefore);

            return new UtilityPaymentDto
            {
                Id = payment.Id,
                ProviderName = provider.Name,
                Category = provider.Category.ToString(),
                SubscriberCode = payment.SubscriberCode,
                Amount = payment.Amount,
                Status = payment.Status,
                CreatedAt = payment.CreatedAt
            };
        }
        catch
        {
            await trx.RollbackAsync();
            throw;
        }
    }

    public async Task<PagedResult<UtilityPaymentDto>> GetMyPaymentsAsync(string userId, PaginationParams pagination)
    {
        var myAccountIds = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.OwnerId == userId)
            .Select(a => a.Id)
            .ToListAsync();

        var query = _context.UtilityPayments
            .AsNoTracking()
            .Include(p => p.Provider)
            .Where(p => myAccountIds.Contains(p.AccountId))
            .OrderByDescending(p => p.CreatedAt);

        var paged = await query.ToPagedResultAsync(pagination);

        return new PagedResult<UtilityPaymentDto>
        {
            Items = paged.Items.Select(p => new UtilityPaymentDto
            {
                Id = p.Id,
                ProviderName = p.Provider.Name,
                Category = p.Provider.Category.ToString(),
                SubscriberCode = p.SubscriberCode,
                Amount = p.Amount,
                Status = p.Status,
                CreatedAt = p.CreatedAt
            }).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages
        };
    }

    public async Task<(byte[] Pdf, string FileName)> GetReceiptPdfAsync(string userId, Guid paymentId)
    {
        var myAccountIds = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.OwnerId == userId)
            .Select(a => a.Id)
            .ToListAsync();

        var payment = await _context.UtilityPayments
            .AsNoTracking()
            .Include(p => p.Provider)
            .FirstOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new NotFoundException(_localizer.Get("PaymentNotFound"));

        if (!myAccountIds.Contains(payment.AccountId))
            throw new BadRequestException(_localizer.Get("PaymentNotYours"));

        var pdf = UtilityReceiptPdfGenerator.Generate(payment);
        var fileName = $"kommunal-qebz-{payment.Id.ToString("N")[..8]}.pdf";
        return (pdf, fileName);
    }

    // DUZELIS 2: GetHashCode() .NET-de her restart-da ferqli reqem verir ->
    // eyni abunechi kodu ucun borc her server restart-da deyiserdi.
    // SHA256 deterministikdir -- eyni giris hemise eyni neticeni verir.
    private static decimal CalculateDebt(string subscriberCode)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(subscriberCode));
        int hash = BitConverter.ToInt32(hashBytes, 0);
        return Math.Abs(hash % 20000) / 100m;
    }

    // provayderi aktiv/deaktiv et (SuperAdmin)
    public async Task<string> ToggleProviderAsync(Guid providerId, bool isActive)
    {
        var provider = await _context.UtilityProviders
            .FirstOrDefaultAsync(p => p.Id == providerId)
            ?? throw new NotFoundException(_localizer.Get("Provaydertaplmad"));

        provider.IsActive = isActive;
        await _context.SaveChangesAsync();

        return $"{provider.Name} {(isActive ? "aktivləşdirildi" : "söndürüldü")}.";
    }
}