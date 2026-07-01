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
    private readonly AppDbContext _context;
    private readonly ILimitService _limitService;

    public UtilityService(AppDbContext context, ILimitService limitService)
    {
        _context = context;
        _limitService = limitService;
    }

    // bütün aktiv provayderlər
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

    // borc sorğusu (simulyasiya — abunəçi kodundan deterministik borc)
    public async Task<DebtResponseDto> GetDebtAsync(DebtInquiryDto dto)
    {
        var provider = await _context.UtilityProviders
            .FirstOrDefaultAsync(p => p.Id == dto.ProviderId && p.IsActive)
            ?? throw new NotFoundException("Provayder tapılmadı.");

        var debt = CalculateDebt(dto.SubscriberCode);

        return new DebtResponseDto
        {
            ProviderName = provider.Name,
            SubscriberCode = dto.SubscriberCode,
            DebtAmount = debt
        };
    }

    // ödəniş
    public async Task<UtilityPaymentDto> PayAsync(string userId, PayUtilityDto dto)
    {
        var provider = await _context.UtilityProviders
            .FirstOrDefaultAsync(p => p.Id == dto.ProviderId && p.IsActive)
            ?? throw new NotFoundException("Provayder tapılmadı.");

        // hesab istifadəçiyə aiddir?
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId && a.OwnerId == userId)
            ?? throw new NotFoundException("Hesab tapılmadı.");

        if (account.Status != AccountStatus.Active)
            throw new BadRequestException("Hesab aktiv deyil.");

        if (account.Balance < dto.Amount)
            throw new BadRequestException("Balans kifayət etmir.");

        // limit yoxlaması (kommunal ödəniş də pul çıxarır)
        await _limitService.EnsureWithinLimitsAsync(account, dto.Amount);

        // atomik: balans çıxması + ödəniş qeydi + transaction qeydi
        await using var trx = await _context.Database.BeginTransactionAsync();

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

        // ümumi transaction tarixçəsinə də əlavə et (çıxan pul kimi)
        _context.Transactions.Add(new Transaction
        {
            Amount = dto.Amount,
            Type = TransactionType.Withdraw,
            Status = TransactionStatus.Completed,
            FromAccountId = account.Id,
            Description = $"Kommunal ödəniş: {provider.Name} ({dto.SubscriberCode})"
        });

        await _context.SaveChangesAsync();
        await trx.CommitAsync();

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

    // ödəniş tarixçəsi (səhifələnmiş)
    public async Task<PagedResult<UtilityPaymentDto>> GetMyPaymentsAsync(
        string userId, PaginationParams pagination)
    {
        // istifadəçinin hesablarının id-ləri
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

    // PDF qəbz
    public async Task<(byte[] Pdf, string FileName)> GetReceiptPdfAsync(string userId, Guid paymentId)
    {
        var myAccountIds = await _context.Accounts
            .Where(a => a.OwnerId == userId)
            .Select(a => a.Id)
            .ToListAsync();

        var payment = await _context.UtilityPayments
            .Include(p => p.Provider)
            .FirstOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new NotFoundException("Ödəniş tapılmadı.");

        // sahiblik yoxlaması
        if (!myAccountIds.Contains(payment.AccountId))
            throw new BadRequestException("Bu ödəniş sizə aid deyil.");

        var pdf = UtilityReceiptPdfGenerator.Generate(payment);
        var fileName = $"kommunal-qebz-{payment.Id.ToString("N")[..8]}.pdf";
        return (pdf, fileName);
    }

    // --- köməkçi: abunəçi kodundan deterministik borc ---
    private static decimal CalculateDebt(string subscriberCode)
    {
        // eyni kod həmişə eyni borcu versin (kodun hash-inə əsaslanır)
        // 0 ilə 200 AZN arası, 2 onluq
        int hash = Math.Abs(subscriberCode.GetHashCode());
        decimal debt = (hash % 20000) / 100m;   // 0.00 - 199.99
        return debt;
    }
}