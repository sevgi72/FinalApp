using BankApiApp.Data;
using BankApiApp.DTOs.Payment;
using BankApiApp.DTOs.Payments;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _context;

    public PaymentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentResultDto> TopUpAsync(string userId, TopUpDto dto)
    {
        // 1. Hesabı tap + sahiblik yoxla
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId)
            ?? throw new Exception("Hesab tapılmadı.");

        if (account.OwnerId != userId)
            throw new Exception("Bu hesab sizə aid deyil.");

        // 2. Məbləğ yoxlaması
        if (dto.Amount <= 0)
            throw new Exception("Məbləğ sıfırdan böyük olmalıdır.");

        // kart nömrəsini təmizlə (boşluqları sil)
        var cardNumber = (dto.CardNumber ?? string.Empty).Replace(" ", "");
        var last4 = cardNumber.Length >= 4 ? cardNumber[^4..] : "0000";

        // 3. Kart validasiyası — uğursuzsa Failed ödəniş logla və qaytar
        string? failure = ValidateCard(cardNumber, dto.ExpiryMonth, dto.ExpiryYear, dto.Cvv);
        if (failure != null)
            return await LogFailedAsync(account.Id, dto.Amount, last4, failure);

        // 4. Ödəniş prosessoru simulyasiyası (kiçik ehtimalla rədd)
        if (!SimulateBankApproval())
            return await LogFailedAsync(account.Id, dto.Amount, last4,
                "Ödəniş bank tərəfindən rədd edildi.");

        // 5. Uğurlu: balansı artır + Deposit əməliyyatı + Payment logu (hamısı bir tranzaksiyada)
        await using var trx = await _context.Database.BeginTransactionAsync();

        account.Balance += dto.Amount;

        _context.Transactions.Add(new Transaction
        {
            Amount = dto.Amount,
            Type = TransactionType.Deposit,
            Status = TransactionStatus.Completed,
            Description = $"Kartla balans artırma (**** {last4})",
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
            Message = "Balans uğurla artırıldı."
        };
    }

    // --- köməkçi metodlar ---

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

    // kartın formal düzgünlüyü
    private static string? ValidateCard(string cardNumber, int month, int year, string cvv)
    {
        if (string.IsNullOrWhiteSpace(cardNumber) || cardNumber.Length != 16 || !cardNumber.All(char.IsDigit))
            return "Kart nömrəsi yanlışdır.";

        if (!IsLuhnValid(cardNumber))
            return "Kart nömrəsi etibarsızdır (Luhn yoxlamasından keçmədi).";

        if (month < 1 || month > 12)
            return "Kartın bitmə ayı yanlışdır.";

        if (string.IsNullOrWhiteSpace(cvv) || cvv.Length != 3 || !cvv.All(char.IsDigit))
            return "CVV yanlışdır.";

        // vaxtı keçməyibmi? (ayın sonuna qədər etibarlı)
        var expiry = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        if (expiry < DateTime.UtcNow.Date)
            return "Kartın vaxtı keçib.";

        return null;   // hər şey qaydasında
    }

    // Luhn yoxlaması — nömrə keçərlidirmi
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

    // ödəniş prosessoru: ~90% uğur (real həyatdakı kimi bəzən rədd)
    private static bool SimulateBankApproval()
        => Random.Shared.Next(0, 10) > 0;   // 1/10 rədd

    private static string GenerateReference()
        => "PAY-" + Guid.NewGuid().ToString("N")[..12].ToUpper();

    public async Task<List<PaymentHistoryDto>> GetMyPaymentsAsync(string userId)
    {
        // istifadəçinin hesablarına aid bütün ödənişlər (yenidən köhnəyə doğru)
        var payments = await _context.Payments
            .Include(p => p.Account)
            .Where(p => p.Account.OwnerId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return payments.Select(p => new PaymentHistoryDto
        {
            Id = p.Id,
            AccountId = p.AccountId,
            Amount = p.Amount,
            CardLast4 = p.CardLast4,
            Status = p.Status.ToString(),
            Reference = p.Reference,
            FailureReason = p.FailureReason,
            CreatedAt = p.CreatedAt
        }).ToList();
    }
}