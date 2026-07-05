using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    // istifadəçi öz söhbətini görür
    [HttpGet("my-messages")]
    public async Task<IActionResult> GetMyMessages()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _chatService.GetMyMessagesAsync(userId));
    }

    // admin bütün söhbətlərin siyahısını görür
    [HttpGet("conversations")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> GetConversations()
        => Ok(await _chatService.GetConversationsAsync());

    // admin konkret istifadəçi ilə söhbəti görür
    [HttpGet("conversation/{userId}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> GetConversation(string userId)
        => Ok(await _chatService.GetConversationAsync(userId));
}