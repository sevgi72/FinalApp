using BankApiApp.Data;
using BankApiApp.DTOs.Card;
using BankApiApp.DTOs.Common;
using BankApiApp.Exceptions;
using BankApiApp.Extensions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class CardService : ICardService
{
    private readonly AppDbContext _context;

    public CardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CardCreatedDto> CreateCardAsync(string userId, CreateCardDto dto)
    {
        // hesabi tap + sahibini yukle
        var account = await _context.Accounts
            .Include(a => a.Owner)
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId)
            ?? throw new NotFoundException("Hesab tapilmadi.");

        if (account.OwnerId != userId)
            throw new BadRequestException("Bu hesab size aid deyil.");

        // unikal kart nomresi yarat (toqqusma olarsa tekrar)
        string cardNumber;
        do
        {
            cardNumber = GenerateCardNumber();
        } while (await _context.Cards.AnyAsync(c => c.CardNumber == cardNumber));

        var cvv = GenerateCvv();
        var now = DateTime.UtcNow;

        var card = new Card
        {
            CardNumber = cardNumber,
            CardHolderName = account.Owner.FullName.ToUpper(),
            ExpiryMonth = now.Month,
            ExpiryYear = now.Year + 4,         // 4 il etibarli
            Status = CardStatus.Active,
            AccountId = account.Id
        };

        _context.Cards.Add(card);
        await _context.SaveChangesAsync();

        // tam nomre + CVV YALNIZ bu cavabda — sonra hec vaxt
        return new CardCreatedDto
        {
            Id = card.Id,
            CardNumber = card.CardNumber,
            Cvv = cvv,
            CardHolderName = card.CardHolderName,
            Expiry = FormatExpiry(card),
            Status = card.Status.ToString()
        };
    }

    public async Task<PagedResult<CardDto>> GetMyCardsAsync(
    string userId, PaginationParams pagination)
    {
        var query = _context.Cards
            .Include(c => c.Account)
            .Where(c => c.Account.OwnerId == userId)
            .OrderByDescending(c => c.CreatedAt);

        var paged = await query.ToPagedResultAsync(pagination);

        return new PagedResult<CardDto>
        {
            Items = paged.Items.Select(MapToDto).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages
        };
    }

    public async Task<CardDto> GetCardAsync(string userId, Guid cardId)
    {
        var card = await LoadOwnedCardAsync(userId, cardId);
        return MapToDto(card);
    }

    public async Task BlockCardAsync(string userId, Guid cardId)
    {
        var card = await LoadOwnedCardAsync(userId, cardId);
        card.Status = CardStatus.Blocked;
        await _context.SaveChangesAsync();
    }

    public async Task UnblockCardAsync(string userId, Guid cardId)
    {
        var card = await LoadOwnedCardAsync(userId, cardId);
        card.Status = CardStatus.Active;
        await _context.SaveChangesAsync();
    }

    // --- komekci metodlar ---

    // sahiblik yoxlamasi ile karti yukle (zencir: Card → Account → OwnerId)
    private async Task<Card> LoadOwnedCardAsync(string userId, Guid cardId)
    {
        var card = await _context.Cards
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.Id == cardId)
            ?? throw new NotFoundException("Kart tapilmadi.");

        if (card.Account.OwnerId != userId)
            throw new BadRequestException("Bu kart size aid deyil.");

        return card;
    }

    private static CardDto MapToDto(Card card) => new()
    {
        Id = card.Id,
        MaskedNumber = Mask(card.CardNumber),
        CardHolderName = card.CardHolderName,
        Expiry = FormatExpiry(card),
        Status = card.Status.ToString(),
        AccountId = card.AccountId
    };

    private static string FormatExpiry(Card card)
        => $"{card.ExpiryMonth:D2}/{card.ExpiryYear % 100:D2}";   // "06/30"

    private static string Mask(string cardNumber)
        => "**** **** **** " + cardNumber[^4..];   // son 4 reqem

    // "4" ile baslayan, Luhn-a uygun 16 reqemli nomre
    private static string GenerateCardNumber()
    {
        var digits = new char[16];
        digits[0] = '4';                                   // Visa-benzer prefiks
        for (int i = 1; i < 15; i++)
            digits[i] = (char)('0' + Random.Shared.Next(0, 10));

        var first15 = new string(digits, 0, 15);
        digits[15] = (char)('0' + LuhnCheckDigit(first15)); // 16-ci = yoxlama reqemi

        return new string(digits);
    }

    // Luhn yoxlama reqemini hesabla
    private static int LuhnCheckDigit(string number)
    {
        int sum = 0;
        bool doubleIt = true;   // sagdan ikiqatlama
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
        return (10 - (sum % 10)) % 10;
    }

    private static string GenerateCvv()
        => Random.Shared.Next(100, 1000).ToString();   // 3 reqem
}