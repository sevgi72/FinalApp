using BankApiApp.DTOs.RoundUp;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Diqqət: Bu controller JWT Token tələb edir
public class RoundUpController : ControllerBase
{
    private readonly IRoundUpService _roundUpService;

    public RoundUpController(IRoundUpService roundUpService)
    {
        _roundUpService = roundUpService;
    }

    // POST /api/RoundUp/enable
    [HttpPost("enable")]
    public async Task<IActionResult> Enable(EnableRoundUpDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _roundUpService.EnableAsync(userId, dto) });
    }

    // POST /api/RoundUp/disable
    [HttpPost("disable")]
    public async Task<IActionResult> Disable()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _roundUpService.DisableAsync(userId) });
    }

    // GET /api/RoundUp/status
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _roundUpService.GetStatusAsync(userId));
    }
}