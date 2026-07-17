using BankApiApp.DTOs.Ai;
using BankApiApp.Helpers;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;

    public AiController(IAiService aiService)
    {
        _aiService = aiService;
    }

    // AI komekciye sual ver
    [HttpPost("ask")]
    [EnableRateLimiting("auth")]   // ← "auth" deyil, "ai" 
    public async Task<IActionResult> Ask(AskAiDto dto)
    {
        var lang = CategoryLocalizer.Normalize(Request.Headers["Accept-Language"].ToString());
        var answer = await _aiService.AskAsync(dto, lang);
        return Ok(new { answer });
    }
}