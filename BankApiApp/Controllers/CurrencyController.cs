using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CurrencyController : ControllerBase
{
    private readonly ICurrencyService _currencyService;

    public CurrencyController(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    // Butun mezenneler (AZN-e gore)
    [HttpGet("rates")]
    public IActionResult GetRates()
    {
        return Ok(_currencyService.GetRates());
    }

    // Cevirme: /api/Currency/convert?from=USD&to=EUR&amount=100
    [HttpGet("convert")]
    public IActionResult Convert(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] decimal amount)
    {
        return Ok(_currencyService.Convert(from, to, amount));
    }
}