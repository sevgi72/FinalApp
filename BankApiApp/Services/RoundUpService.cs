using BankApiApp.Data;
using BankApiApp.DTOs.RoundUp;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class RoundUpService : IRoundUpService
{
    private readonly ILocalizationService _localizer;
    private readonly AppDbContext _context;

    // round-up kocurmelerini tarixcede taniyan isare
    public const string RoundUpDescription = "Round-up yigim";

    public RoundUpService(AppDbContext context, ILocalizationService localizer)
    {
        _localizer = localizer;
        _context = context;
    }

    public async Task<string> EnableAsync(string userId, EnableRoundUpDto dto)
    {
        if (dto.RoundTo != 1 && dto.RoundTo != 5)
            throw new BadRequestException(_localizer.Get("RoundingMustBe1Or5"));

        if (dto.SourceAccountId == dto.TargetAccountId)
            throw new BadRequestException(_localizer.Get("SourceAndSavingsAccountCannotBeSame"));

        // her iki hesab istifadeciye aiddir?
        var source = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.SourceAccountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("SourceAccountNotFound"));

        var target = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.TargetAccountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("SavingsAccountNotFound"));

        // ayar var? yenile : yarat
        var setting = await _context.RoundUpSettings
            .FirstOrDefaultAsync(r => r.UserId == userId);

        if (setting == null)
        {
            setting = new RoundUpSetting { UserId = userId };
            _context.RoundUpSettings.Add(setting);
        }

        setting.SourceAccountId = dto.SourceAccountId;
        setting.TargetAccountId = dto.TargetAccountId;
        setting.RoundTo = dto.RoundTo;
        setting.IsEnabled = true;

        await _context.SaveChangesAsync();

        return $"Round-up yigim aktivlesdirildi ({dto.RoundTo} AZN-e yuvarlaqlasdirma).";
    }

    public async Task<string> DisableAsync(string userId)
    {
        var setting = await _context.RoundUpSettings
            .FirstOrDefaultAsync(r => r.UserId == userId)
            ?? throw new NotFoundException(_localizer.Get("RoundUpSettingNotFound"));

        setting.IsEnabled = false;
        await _context.SaveChangesAsync();

        return "Round-up yigim sondurulду.";
    }

    public async Task<RoundUpStatusDto> GetStatusAsync(string userId)
    {
        var setting = await _context.RoundUpSettings
            .FirstOrDefaultAsync(r => r.UserId == userId);

        if (setting == null)
            return new RoundUpStatusDto { IsEnabled = false };

        // round-up kocurmelerinin cemi (target hesaba, bizim isare ile)
        var roundUpQuery = _context.Transactions
            .Where(t => t.ToAccountId == setting.TargetAccountId
                     && t.Description == RoundUpDescription
                     && t.Status == TransactionStatus.Completed);

        var monthStart = DateTime.SpecifyKind(
            new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1), DateTimeKind.Utc);

        var totalAll = await roundUpQuery.SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var totalMonth = await roundUpQuery
            .Where(t => t.CreatedAt >= monthStart)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        return new RoundUpStatusDto
        {
            IsEnabled = setting.IsEnabled,
            SourceAccountId = setting.SourceAccountId,
            TargetAccountId = setting.TargetAccountId,
            RoundTo = setting.RoundTo,
            TotalSavedThisMonth = totalMonth,
            TotalSavedAllTime = totalAll
        };
    }

    // XERC EMELIYYATINDAN SONRA CAGIRILIR
    // hec vaxt exception atmir — esas emeliyyati poza bilmez
    public async Task TryProcessAsync(Account sourceAccount, decimal spentAmount)
    {
        try
        {
            // bu hesab ucun aktiv round-up var?
            var setting = await _context.RoundUpSettings
                .FirstOrDefaultAsync(r => r.SourceAccountId == sourceAccount.Id && r.IsEnabled);

            if (setting == null)
                return;   // round-up yoxdur — sessizce cix

            // ferqi hesabla: ceiling(4.30 / 1) * 1 - 4.30 = 0.70
            var roundTo = (decimal)setting.RoundTo;
            var difference = Math.Ceiling(spentAmount / roundTo) * roundTo - spentAmount;

            if (difference <= 0)
                return;   // mebleg onsuz tam ededdir — yigim yoxdur

            // qaliq balans ferqe catir? catmirsa sessizce atla (alisa mane olma!)
            if (sourceAccount.Balance < difference)
                return;

            // yigim hesabi hele movcud ve aktivdir?
            var target = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == setting.TargetAccountId
                                       && a.Status == AccountStatus.Active);
            if (target == null)
                return;

            // ferqi kocur: menbe → yigim
            sourceAccount.Balance -= difference;
            target.Balance += difference;

            _context.Transactions.Add(new Transaction
            {
                Amount = difference,
                Type = TransactionType.Transfer,
                Status = TransactionStatus.Completed,
                FromAccountId = sourceAccount.Id,
                ToAccountId = target.Id,
                Description = RoundUpDescription
            });

            await _context.SaveChangesAsync();
        }
        catch
        {
            // round-up xetasi esas emeliyyati POZMAMALIDIR — sessizce udulur
            // (loglamaq istesen ILogger inject ede bilerik)
        }
    }
}