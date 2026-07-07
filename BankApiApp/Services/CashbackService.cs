using BankApiApp.Data;
using BankApiApp.DTOs.Cashback;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class CashbackService : ICashbackService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILocalizationService _localizer;

    // kesbek emeliyyatlarini taniyan isare (statistika ucun)
    public const string CashbackDescription = "Kesbek bonus";

    public CashbackService(AppDbContext context,
        UserManager<ApplicationUser> userManager,
        ILocalizationService localizer)
    {
        _context = context;
        _userManager = userManager;
        _localizer = localizer;
    }

    // kateqoriyaya gore kesbek faizi
    private static decimal GetRate(string category) => category switch
    {
        "Kommunal" => 0.05m,       // kommunal 5%
        "Nagd cixaris" => 0.01m,   // nagd 1%
        _ => 0m                    // digerlerine kesbek yoxdur
    };

    // emeliyyatdan sonra kesbek hesabla (hec vaxt exception atmir)
    public async Task TryEarnAsync(string userId, decimal spentAmount, string category)
    {
        try
        {
            var rate = GetRate(category);
            if (rate <= 0) return;

            var cashback = Math.Round(spentAmount * rate, 2);
            if (cashback <= 0) return;

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return;

            user.CashbackBalance += cashback;
            await _userManager.UpdateAsync(user);

            // kesbek qeydi (statistika ucun transaction kimi - sadece izleme)
            _context.Transactions.Add(new Transaction
            {
                Amount = cashback,
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed,
                Description = CashbackDescription
            });
            await _context.SaveChangesAsync();
        }
        catch
        {
            // kesbek xetasi esas emeliyyati pozmamalidir
        }
    }

    public async Task<CashbackStatusDto> GetStatusAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(_localizer.Get("UserNotFound"));

        var monthStart = DateTime.SpecifyKind(
            new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1), DateTimeKind.Utc);

        var earnedThisMonth = await _context.Transactions
            .Where(t => t.Description == CashbackDescription && t.CreatedAt >= monthStart)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        return new CashbackStatusDto
        {
            CashbackBalance = user.CashbackBalance,
            TotalEarnedThisMonth = earnedThisMonth
        };
    }

    // bonus balansi esas hesaba kocur
    public async Task<string> RedeemAsync(string userId, Guid targetAccountId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(_localizer.Get("UserNotFound"));

        if (user.CashbackBalance <= 0)
            throw new BadRequestException("Kesbek balansi bosdur.");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == targetAccountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        if (account.Status != AccountStatus.Active)
            throw new BadRequestException(_localizer.Get("AccountNotActive"));

        var amount = user.CashbackBalance;

        await using var trx = await _context.Database.BeginTransactionAsync();

        account.Balance += amount;
        user.CashbackBalance = 0;
        await _userManager.UpdateAsync(user);

        _context.Transactions.Add(new Transaction
        {
            Amount = amount,
            Type = TransactionType.Deposit,
            Status = TransactionStatus.Completed,
            ToAccountId = account.Id,
            Description = "Kesbek balansi kocuruldu"
        });

        await _context.SaveChangesAsync();
        await trx.CommitAsync();

        return $"{amount:0.00} AZN kesbek hesabiniza kocuruldu.";
    }
}