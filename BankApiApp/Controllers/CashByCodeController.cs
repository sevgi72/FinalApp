using BankApiApp.DTOs.CashByCode;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CashByCodeController : ControllerBase
{
    private readonly ICashByCodeService _service;

    public CashByCodeController(ICashByCodeService service)
    {
        _service = service;
    }

    // kod yarat (pul gonder)
    [HttpPost]
    public async Task<IActionResult> Create(CreateCashByCodeDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.CreateAsync(userId, dto));
    }

    // kodla pulu gotur
    [HttpPost("claim")]
    public async Task<IActionResult> Claim(ClaimCashByCodeDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _service.ClaimAsync(userId, dto) });
    }

    // kodu legv et (pul qayidir)
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _service.CancelAsync(userId, id) });
    }

    // menim kodlarim
    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.GetMyAsync(userId));
    }
}