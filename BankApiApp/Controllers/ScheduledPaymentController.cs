using BankApiApp.DTOs.Utility;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScheduledPaymentController : ControllerBase
{
    private readonly IScheduledPaymentService _service;

    public ScheduledPaymentController(IScheduledPaymentService service)
    {
        _service = service;
    }

    // yeni planlanmis odenis yarat
    [HttpPost]
    public async Task<IActionResult> Create(CreateScheduledPaymentDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.CreateAsync(userId, dto));
    }

    // menim planlanmis odenislerim
    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.GetMyAsync(userId));
    }

    // aktiv/deaktiv et
    [HttpPost("{id}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, [FromQuery] bool isActive)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _service.ToggleAsync(userId, id, isActive) });
    }

    // sil
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _service.DeleteAsync(userId, id) });
    }
}