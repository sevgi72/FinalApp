using BankApiApp.DTOs.Card;
using BankApiApp.DTOs.Common;

namespace BankApiApp.Services;

public interface ICardService
{
    Task<CardCreatedDto> CreateCardAsync(string userId, CreateCardDto dto);
    Task<PagedResult<CardDto>> GetMyCardsAsync(string userId, PaginationParams pagination);
    Task<CardDto> GetCardAsync(string userId, Guid cardId);
    Task BlockCardAsync(string userId, Guid cardId);
    Task UnblockCardAsync(string userId, Guid cardId);
}