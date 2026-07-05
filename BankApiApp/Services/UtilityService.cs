using BankApiApp.Data;
using BankApiApp.DTOs.Common;
using BankApiApp.DTOs.Utility;
using BankApiApp.Exceptions;
using BankApiApp.Extensions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class UtilityService : IUtilityService
{
    private readonly ILocalizationService _localizer;
    private readonly AppDbContext _context;
    private readonly ILimitService _limitService;
    private readonly IRoundUpService _roundUpService;

    public UtilityService(AppDbContext context, ILimitService limitService, IRoundUpService roundUpService, ILocalizationService localizer)
    {
        _localizer = localizer;
        _context = context;
        _limitService = limitService;
        _roundUpService = roundUpService;
    }

    public async Task<List<UtilityProviderDto>> GetProvidersAsync()
    {
        var providers = await _context.UtilityProviders
            .Where(p => p.IsActive)
            .OrderBy(p => p.Category)
            .ToListAsync();

        return providers.Select(p => new UtilityProviderDto
        {
            Id = p.Id,
            Name = p.Name,
            Category = p.Category.ToString()
        }).ToList();
    }

    public async Task<DebtResponseDto> GetDebtAsync(DebtInquiryDto dto)
    {
        var provider = await _context.UtilityProviders
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
                Description = $"Kommunal odenis: {provider.Name} ({dto.SubscriberCode})"
            });

            await _context.SaveChangesAsync();
            await trx.CommitAsync();

            // [Round-up Qoşulması]: Kommunal ödəniş uğurla bitdikdən sonra yuvarlaqlaşdırma yığımı
            await _roundUpService.TryProcessAsync(account, dto.Amount);

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
            .Where(a => a.OwnerId == userId)
            .Select(a => a.Id)
            .ToListAsync();

        var query = _context.UtilityPayments
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
            .Where(a => a.OwnerId == userId)
            .Select(a => a.Id)
            .ToListAsync();

        var payment = await _context.UtilityPayments
            .Include(p => p.Provider)
            .FirstOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new NotFoundException(_localizer.Get("PaymentNotFound"));

        if (!myAccountIds.Contains(payment.AccountId))
            throw new BadRequestException(_localizer.Get("PaymentNotYours"));

        var pdf = UtilityReceiptPdfGenerator.Generate(payment);
        var fileName = $"kommunal-qebz-{payment.Id.ToString("N")[..8]}.pdf";
        return (pdf, fileName);
    }

    private static decimal CalculateDebt(string subscriberCode)
    {
        int hash = Math.Abs(subscriberCode.GetHashCode());
        decimal debt = (hash % 20000) / 100m;
        return debt;
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