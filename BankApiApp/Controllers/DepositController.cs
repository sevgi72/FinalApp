using BankApiApp.DTOs.Deposit;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DepositController : ControllerBase
{
    private readonly IDepositService _service;

    public DepositController(IDepositService service)
    {
        _service = service;
    }

    // emanet ac
    [HttpPost]
    public async Task<IActionResult> Open(CreateDepositDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.OpenAsync(userId, dto));
    }

    // menim emanetlerim
    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.GetMyAsync(userId));
    }

    // emaneti bagla (pul + faiz qaytar)
    [HttpPost("{id}/close")]
    public async Task<IActionResult> Close(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _service.CloseAsync(userId, id) });
    }
}