using BankApiApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestLocalizationController : ControllerBase
{
    private readonly ILocalizationService _localizer;

    public TestLocalizationController(ILocalizationService localizer)
    {
        _localizer = localizer;
    }

    [HttpGet("balance-message")]
    public IActionResult GetBalanceMessage()
        => Ok(new { message = _localizer.Get("InsufficientBalance") });
}