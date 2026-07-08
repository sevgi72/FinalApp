using BankApiApp.DTOs.Statement;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StatementController : ControllerBase
{
    private readonly IStatementService _service;

    public StatementController(IStatementService service)
    {
        _service = service;
    }

    // hesabat (JSON)
    [HttpPost]
    public async Task<IActionResult> Generate(StatementRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.GenerateAsync(userId, dto));
    }

    // hesabat (PDF yukle)
    [HttpPost("pdf")]
    public async Task<IActionResult> GeneratePdf(StatementRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var pdf = await _service.GeneratePdfAsync(userId, dto);
        return File(pdf, "application/pdf", "statement.pdf");
    }
}