using BankApiApp.DTOs.Common;
using BankApiApp.DTOs.Payment;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("topup")]
    public async Task<IActionResult> TopUp(TopUpDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _paymentService.TopUpAsync(userId, dto));
    }

    [HttpGet("my")]
    public async Task<IActionResult> My([FromQuery] PaginationParams pagination)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _paymentService.GetMyPaymentsAsync(userId, pagination));
    }
}