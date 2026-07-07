using BankApiApp.DTOs.Cashback;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CashbackController : ControllerBase
{
    private readonly ICashbackService _service;

    public CashbackController(ICashbackService service)
    {
        _service = service;
    }

    // kesbek balansi ve statistika
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.GetStatusAsync(userId));
    }

    // bonusu esas hesaba kocur
    [HttpPost("redeem")]
    public async Task<IActionResult> Redeem(RedeemCashbackDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _service.RedeemAsync(userId, dto.TargetAccountId) });
    }
}