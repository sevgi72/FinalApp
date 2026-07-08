using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BalanceAlertController : ControllerBase
{
    private readonly IBalanceAlertService _service;

    public BalanceAlertController(IBalanceAlertService service)
    {
        _service = service;
    }

    // asagi balans heddi teyin et (threshold=null gonderilse legv olunur)
    [HttpPost("threshold")]
    public async Task<IActionResult> SetThreshold(
        [FromQuery] Guid accountId, [FromQuery] decimal? threshold)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _service.SetThresholdAsync(userId, accountId, threshold) });
    }
}