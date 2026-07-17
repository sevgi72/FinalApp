using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]   // ← rol telebi silindi: her metod ozu teyin edir
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    // istifadeci OZ sohbetini gorur (rol telebi yoxdur)
    // userId token-den goturulur — bashqasinin yazismasini oxumaq mumkun deyil
    // istifadeci OZ sohbetini gorur (rol telebi yoxdur).
    // userId token-den goturulur — bashqasinin yazismasini oxumaq mumkun deyil.
    // days: son nece gun (default 7, max 30)
    [HttpGet("my-messages")]
    public async Task<IActionResult> GetMyMessages([FromQuery] int days = 7)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _chatService.GetMyMessagesAsync(userId, days));
    }

    // admin butun sohbetlerin siyahisini gorur
    [HttpGet("conversations")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> GetConversations()
        => Ok(await _chatService.GetConversationsAsync());

    // admin konkret istifadeci ile sohbeti gorur
    [HttpGet("conversation/{userId}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> GetConversation(string userId)
        => Ok(await _chatService.GetConversationAsync(userId));
}