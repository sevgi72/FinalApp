using BankApiApp.DTOs.Common;
using BankApiApp.DTOs.Utility;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UtilityController : ControllerBase
{
    private readonly IUtilityService _utilityService;

    public UtilityController(IUtilityService utilityService)
    {
        _utilityService = utilityService;
    }

    // butun provayderler
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders()
        => Ok(await _utilityService.GetProvidersAsync());

    // borc sorgusu
    [HttpPost("debt")]
    public async Task<IActionResult> GetDebt(DebtInquiryDto dto)
        => Ok(await _utilityService.GetDebtAsync(dto));

    // odenis
    [HttpPost("pay")]
    public async Task<IActionResult> Pay(PayUtilityDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _utilityService.PayAsync(userId, dto));
    }

    // odenis tarixcesi
    [HttpGet("my")]
    public async Task<IActionResult> GetMyPayments([FromQuery] PaginationParams pagination)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _utilityService.GetMyPaymentsAsync(userId, pagination));
    }

    // PDF qebz
    [HttpGet("{id}/receipt/pdf")]
    public async Task<IActionResult> GetReceiptPdf(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var (pdf, fileName) = await _utilityService.GetReceiptPdfAsync(userId, id);
        return File(pdf, "application/pdf", fileName);
    }
}