using BankApiApp.Data;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class BalanceAlertService : IBalanceAlertService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizer;

    public BalanceAlertService(AppDbContext context,
        INotificationService notificationService,
        ILocalizationService localizer)
    {
        _context = context;
        _notificationService = notificationService;
        _localizer = localizer;
    }

    // istifadeci asagi balans heddini teyin edir
    public async Task<string> SetThresholdAsync(string userId, Guid accountId, decimal? threshold)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        if (threshold.HasValue && threshold.Value < 0)
            throw new BadRequestException("Hedd menfi ola bilmez.");

        account.LowBalanceThreshold = threshold;
        await _context.SaveChangesAsync();

        return threshold.HasValue
            ? $"Asagi balans xeberdarligi {threshold.Value:0.00} AZN olaraq teyin edildi."
            : "Asagi balans xeberdarligi legv edildi.";
    }

    // emeliyyatdan sonra yoxla (hec vaxt exception atmir)
    public async Task CheckAndAlertAsync(Account account, decimal balanceBefore)
    {
        try
        {
            // hedd teyin olunmayibsa - hec ne etme
            if (!account.LowBalanceThreshold.HasValue)
                return;

            var threshold = account.LowBalanceThreshold.Value;

            // yalniz hedden YENICE kecende bildir
            // (evvel hedden yuxari idi, indi asagi dusdu)
            bool wasAbove = balanceBefore >= threshold;
            bool nowBelow = account.Balance < threshold;

            if (wasAbove && nowBelow)
            {
                await _notificationService.SendToUserAsync(account.OwnerId,
                    "Asagi balans xeberdarligi",
                    $"{account.AccountNumber} nomreli hesabinizin balansi {account.Balance:0.00} AZN-e dusub (hedd: {threshold:0.00} AZN).",
                    "System");
            }
        }
        catch
        {
            // xeberdarliq xetasi esas emeliyyati pozmamalidir
        }
    }
}