using BankApiApp.DTOs.Card;
using BankApiApp.DTOs.Common;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CardController : ControllerBase
{
    private readonly ICardService _cardService;

    public CardController(ICardService cardService)
    {
        _cardService = cardService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCardDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _cardService.CreateCardAsync(userId, dto));
    }

    [HttpGet("my")]
    public async Task<IActionResult> My([FromQuery] PaginationParams pagination)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _cardService.GetMyCardsAsync(userId, pagination));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _cardService.GetCardAsync(userId, id));
    }

    [HttpPost("{id}/block")]
    public async Task<IActionResult> Block(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _cardService.BlockCardAsync(userId, id);
        return Ok(new { message = "Kart bloklandı." });
    }

    [HttpPost("{id}/unblock")]
    public async Task<IActionResult> Unblock(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _cardService.UnblockCardAsync(userId, id);
        return Ok(new { message = "Kart aktivləşdirildi." });
    }
}