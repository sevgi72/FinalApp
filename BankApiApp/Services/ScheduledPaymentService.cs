using BankApiApp.Data;
using BankApiApp.DTOs.Utility;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class ScheduledPaymentService : IScheduledPaymentService
{
    private readonly AppDbContext _context;
    private readonly ILocalizationService _localizer;
    private readonly ILogger<ScheduledPaymentService> _logger;

    public ScheduledPaymentService(AppDbContext context,
        ILocalizationService localizer,
        ILogger<ScheduledPaymentService> logger)
    {
        _context = context;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<ScheduledPaymentDto> CreateAsync(string userId, CreateScheduledPaymentDto dto)
    {
        // hesab istifadeciye aiddir?
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        var provider = await _context.UtilityProviders
            .FirstOrDefaultAsync(p => p.Id == dto.ProviderId && p.IsActive)
            ?? throw new NotFoundException(_localizer.Get("ProviderNotFound"));

        var sp = new ScheduledPayment
        {
            UserId = userId,
            AccountId = dto.AccountId,
            ProviderId = dto.ProviderId,
            SubscriberCode = dto.SubscriberCode,
            Amount = dto.Amount,
            DayOfMonth = dto.DayOfMonth,
            IsActive = true
        };

        _context.ScheduledPayments.Add(sp);
        await _context.SaveChangesAsync();

        return new ScheduledPaymentDto
        {
            Id = sp.Id,
            ProviderName = provider.Name,
            SubscriberCode = sp.SubscriberCode,
            Amount = sp.Amount,
            DayOfMonth = sp.DayOfMonth,
            IsActive = sp.IsActive,
            LastExecutedAt = sp.LastExecutedAt
        };
    }

    public async Task<List<ScheduledPaymentDto>> GetMyAsync(string userId)
    {
        return await _context.ScheduledPayments
            .Include(s => s.Provider)
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.DayOfMonth)
            .Select(s => new ScheduledPaymentDto
            {
                Id = s.Id,
                ProviderName = s.Provider.Name,
                SubscriberCode = s.SubscriberCode,
                Amount = s.Amount,
                DayOfMonth = s.DayOfMonth,
                IsActive = s.IsActive,
                LastExecutedAt = s.LastExecutedAt
            })
            .ToListAsync();
    }

    public async Task<string> ToggleAsync(string userId, Guid id, bool isActive)
    {
        var sp = await _context.ScheduledPayments
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId)
            ?? throw new NotFoundException(_localizer.Get("PaymentNotFound"));

        sp.IsActive = isActive;
        await _context.SaveChangesAsync();

        return isActive ? "Planlanmis odenis aktivlesdirildi." : "Planlanmis odenis dayandirildi.";
    }

    public async Task<string> DeleteAsync(string userId, Guid id)
    {
        var sp = await _context.ScheduledPayments
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId)
            ?? throw new NotFoundException(_localizer.Get("PaymentNotFound"));

        _context.ScheduledPayments.Remove(sp);
        await _context.SaveChangesAsync();

        return "Planlanmis odenis silindi.";
    }

    // HANGFIRE: her gun isleyir — bu gun odenmeli olanlari ode
    public async Task ProcessDuePaymentsAsync()
    {
        var today = DateTime.UtcNow;
        var todayDay = today.Day;
        var monthStart = DateTime.SpecifyKind(new DateTime(today.Year, today.Month, 1), DateTimeKind.Utc);

        // bu gun icra olunmali VE bu ay hele icra olunmamis qaydalar
        var due = await _context.ScheduledPayments
            .Include(s => s.Provider)
            .Where(s => s.IsActive
                     && s.DayOfMonth == todayDay
                     && (s.LastExecutedAt == null || s.LastExecutedAt < monthStart))
            .ToListAsync();

        if (!due.Any())
        {
            _logger.LogInformation("Planlanmis odenisler: bu gun icra olunacaq yoxdur.");
            return;
        }

        foreach (var sp in due)
        {
            try
            {
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == sp.AccountId && a.Status == AccountStatus.Active);

                // hesab yoxdur/deaktiv ve ya balans catmir → atla (novbeti ay yene cehd)
                if (account == null || account.Balance < sp.Amount)
                {
                    _logger.LogWarning("Planlanmis odenis atlandi (balans/hesab): {Id}", sp.Id);
                    continue;
                }

                await using var trx = await _context.Database.BeginTransactionAsync();

                account.Balance -= sp.Amount;

                _context.UtilityPayments.Add(new UtilityPayment
                {
                    ProviderId = sp.ProviderId,
                    SubscriberCode = sp.SubscriberCode,
                    Amount = sp.Amount,
                    AccountId = account.Id,
                    Status = "Completed"
                });

                _context.Transactions.Add(new Transaction
                {
                    Amount = sp.Amount,
                    Type = TransactionType.Withdraw,
                    Status = TransactionStatus.Completed,
                    FromAccountId = account.Id,
                    Description = $"Kommunal ödəniş (avtomatik): {sp.Provider.Name} ({sp.SubscriberCode})"
                });

                sp.LastExecutedAt = today;

                await _context.SaveChangesAsync();
                await trx.CommitAsync();

                _logger.LogInformation("Planlanmis odenis icra olundu: {Provider} {Amount} AZN", sp.Provider.Name, sp.Amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Planlanmis odenis xetasi: {Id}", sp.Id);
                // bir qaydanin xetasi digerlerini dayandirmasin
            }
        }
    }
}