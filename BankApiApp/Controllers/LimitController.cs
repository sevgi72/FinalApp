using BankApiApp.DTOs.Limits;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LimitController : ControllerBase
{
    private readonly ILimitService _limitService;

    public LimitController(ILimitService limitService)
    {
        _limitService = limitService;
    }

    [HttpGet("{accountId}")]
    public async Task<IActionResult> Get(Guid accountId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _limitService.GetLimitInfoAsync(userId, accountId));
    }

    [HttpPut("{accountId}")]
    public async Task<IActionResult> Update(Guid accountId, UpdateLimitsDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _limitService.UpdateLimitsAsync(userId, accountId, dto));
    }
}