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

    // bütün provayderlər
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders()
        => Ok(await _utilityService.GetProvidersAsync());

    // borc sorğusu
    [HttpPost("debt")]
    public async Task<IActionResult> GetDebt(DebtInquiryDto dto)
        => Ok(await _utilityService.GetDebtAsync(dto));

    // ödəniş
    [HttpPost("pay")]
    public async Task<IActionResult> Pay(PayUtilityDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _utilityService.PayAsync(userId, dto));
    }

    // ödəniş tarixçəsi
    [HttpGet("my")]
    public async Task<IActionResult> GetMyPayments([FromQuery] PaginationParams pagination)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _utilityService.GetMyPaymentsAsync(userId, pagination));
    }

    // PDF qəbz
    [HttpGet("{id}/receipt/pdf")]
    public async Task<IActionResult> GetReceiptPdf(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var (pdf, fileName) = await _utilityService.GetReceiptPdfAsync(userId, id);
        return File(pdf, "application/pdf", fileName);
    }
}