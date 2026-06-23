using BankApiApp.DTOs.Card;

namespace BankApiApp.Services;

public interface ICardService
{
    Task<CardCreatedDto> CreateCardAsync(string userId, CreateCardDto dto);
    Task<List<CardDto>> GetMyCardsAsync(string userId);
    Task<CardDto> GetCardAsync(string userId, Guid cardId);
    Task BlockCardAsync(string userId, Guid cardId);
    Task UnblockCardAsync(string userId, Guid cardId);
}