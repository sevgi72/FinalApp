using BankApiApp.DTOs.Utility;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentTemplateController : ControllerBase
{
    private readonly IPaymentTemplateService _service;

    public PaymentTemplateController(IPaymentTemplateService service)
    {
        _service = service;
    }

    // yeni sablon yarat
    [HttpPost]
    public async Task<IActionResult> Create(CreatePaymentTemplateDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.CreateAsync(userId, dto));
    }

    // menim sablonlarim
    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.GetMyAsync(userId));
    }

    // sablonu sil
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _service.DeleteAsync(userId, id) });
    }
}