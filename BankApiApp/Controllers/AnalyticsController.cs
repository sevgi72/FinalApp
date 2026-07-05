using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _analyticsService.GetDashboardAsync(userId));
    }


    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
    [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _analyticsService.GetSummaryAsync(userId, startDate, endDate));
    }

    // xerc kateqoriyalari (bu ay ve ya secilen ay)
    [HttpGet("spending")]
    [Authorize]
    public async Task<IActionResult> GetSpending([FromQuery] int? year, [FromQuery] int? month)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _analyticsService.GetSpendingByCategoryAsync(userId, year, month));
    }

    // son N ayin xerc trendi
    [HttpGet("spending/monthly")]
    [Authorize]
    public async Task<IActionResult> GetMonthlySpending([FromQuery] int months = 6)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _analyticsService.GetMonthlySpendingAsync(userId, months));
    }
}