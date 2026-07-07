using BankApiApp.DTOs.Notification;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _service;

    public NotificationController(INotificationService service)
    {
        _service = service;
    }

    // menim bildirislerim
    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.GetMyAsync(userId));
    }

    // oxunmamis say (zeng ikonu ucun)
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { count = await _service.GetUnreadCountAsync(userId) });
    }

    // oxunmus isarele
    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _service.MarkAsReadAsync(userId, id) });
    }

    // hamisini oxunmus isarele
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _service.MarkAllReadAsync(userId) });
    }

    // admin - hamiya elan
    [HttpPost("broadcast")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Broadcast(BroadcastNotificationDto dto)
        => Ok(new { message = await _service.BroadcastAsync(dto) });
}