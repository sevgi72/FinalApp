using BankApiApp.Data;
using BankApiApp.DTOs.Common;
using BankApiApp.DTOs.Payment;
using BankApiApp.DTOs.Payments;
using BankApiApp.Exceptions;
using BankApiApp.Extensions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class PaymentService : IPaymentService
{
    private readonly ILocalizationService _localizer;
    private readonly AppDbContext _context;

    public PaymentService(AppDbContext context, ILocalizationService localizer)
    {
        _localizer = localizer;
        _context = context;
    }

    public async Task<PaymentResultDto> TopUpAsync(string userId, TopUpDto dto)
    {
        // 1. Hesabi tap + sahiblik yoxla
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        if (account.OwnerId != userId)
            throw new BadRequestException(_localizer.Get("AccountNotYours"));

        // 2. Mebleg yoxlamasi
        if (dto.Amount <= 0)
            throw new BadRequestException(_localizer.Get("AmountMustBePositive"));

        // kart nomresini temizle (bosluqlari sil)
        var cardNumber = (dto.CardNumber ?? string.Empty).Replace(" ", "");
        var last4 = cardNumber.Length >= 4 ? cardNumber[^4..] : "0000";

        // 3. Kart validasiyasi — ugursuzsa Failed odenis logla ve qaytar
        string? failure = ValidateCard(cardNumber, dto.ExpiryMonth, dto.ExpiryYear, dto.Cvv);
        if (failure != null)
            return await LogFailedAsync(account.Id, dto.Amount, last4, failure);

        // 4. Odenis prosessoru simulyasiyasi (kicik ehtimalla redd)
        if (!SimulateBankApproval())
            return await LogFailedAsync(account.Id, dto.Amount, last4,
                "Odenis bank terefinden redd edildi.");

        // 5. Ugurlu: balansi artir + Deposit emeliyyati + Payment logu (hamisi bir tranzaksiyada)
        await using var trx = await _context.Database.BeginTransactionAsync();

        account.Balance += dto.Amount;

        _context.Transactions.Add(new Transaction
        {
            Amount = dto.Amount,
            Type = TransactionType.Deposit,
            Status = TransactionStatus.Completed,
            Description = $"Kartla balans artirma (**** {last4})",
            ToAccountId = account.Id
        });

        var reference = GenerateReference();
        _context.Payments.Add(new Payment
        {
            AccountId = account.Id,
            Amount = dto.Amount,
            CardLast4 = last4,
            Status = PaymentStatus.Completed,
            Reference = reference
        });

        await _context.SaveChangesAsync();
        await trx.CommitAsync();

        return new PaymentResultDto
        {
            Success = true,
            Status = PaymentStatus.Completed.ToString(),
            Reference = reference,
            Amount = dto.Amount,
            NewBalance = account.Balance,
            Message = "Balans ugurla artirildi."
        };
    }

    // --- komekci metodlar ---

    private async Task<PaymentResultDto> LogFailedAsync(
        Guid accountId, decimal amount, string last4, string reason)
    {
        var reference = GenerateReference();
        _context.Payments.Add(new Payment
        {
            AccountId = accountId,
            Amount = amount,
            CardLast4 = last4,
            Status = PaymentStatus.Failed,
            Reference = reference,
            FailureReason = reason
        });
        await _context.SaveChangesAsync();

        return new PaymentResultDto
        {
            Success = false,
            Status = PaymentStatus.Failed.ToString(),
            Reference = reference,
            Amount = amount,
            NewBalance = 0,
            Message = reason
        };
    }

    // kartin formal duzgunluyu
    private static string? ValidateCard(string cardNumber, int month, int year, string cvv)
    {
        if (string.IsNullOrWhiteSpace(cardNumber) || cardNumber.Length != 16 || !cardNumber.All(char.IsDigit))
            return "Kart nomresi yanlisdir.";

        if (!IsLuhnValid(cardNumber))
            return "Kart nomresi etibarsizdir (Luhn yoxlamasindan kecmedi).";

        if (month < 1 || month > 12)
            return "Kartin bitme ayi yanlisdir.";

        if (string.IsNullOrWhiteSpace(cvv) || cvv.Length != 3 || !cvv.All(char.IsDigit))
            return "CVV yanlisdir.";

        // vaxti kecmeyibmi? (ayin sonuna qeder etibarli)
        var expiry = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        if (expiry < DateTime.UtcNow.Date)
            return "Kartin vaxti kecib.";

        return null;   // her sey qaydasinda
    }

    // Luhn yoxlamasi — nomre kecerlidirmi
    private static bool IsLuhnValid(string number)
    {
        int sum = 0;
        bool doubleIt = false;
        for (int i = number.Length - 1; i >= 0; i--)
        {
            int d = number[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
            doubleIt = !doubleIt;
        }
        return sum % 10 == 0;
    }

    // odenis prosessoru: ~90% ugur (real heyatdaki kimi bezen redd)
    private static bool SimulateBankApproval()
        => Random.Shared.Next(0, 10) > 0;   // 1/10 redd

    private static string GenerateReference()
        => "PAY-" + Guid.NewGuid().ToString("N")[..12].ToUpper();

    public async Task<PagedResult<PaymentHistoryDto>> GetMyPaymentsAsync(
    string userId, PaginationParams pagination)
    {
        var query = _context.Payments
            .Include(p => p.Account)
            .Where(p => p.Account.OwnerId == userId)
            .OrderByDescending(p => p.CreatedAt);

        var paged = await query.ToPagedResultAsync(pagination);

        return new PagedResult<PaymentHistoryDto>
        {
            Items = paged.Items.Select(p => new PaymentHistoryDto
            {
                Id = p.Id,
                AccountId = p.AccountId,
                Amount = p.Amount,
                CardLast4 = p.CardLast4,
                Status = p.Status.ToString(),
                Reference = p.Reference,
                FailureReason = p.FailureReason,
                CreatedAt = p.CreatedAt
            }).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages
        };
    }
}