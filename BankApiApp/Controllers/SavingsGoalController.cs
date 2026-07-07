using BankApiApp.DTOs.SavingsGoal;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SavingsGoalController : ControllerBase
{
    private readonly ISavingsGoalService _service;

    public SavingsGoalController(ISavingsGoalService service)
    {
        _service = service;
    }

    // yeni meqsed
    [HttpPost]
    public async Task<IActionResult> Create(CreateSavingsGoalDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.CreateAsync(userId, dto));
    }

    // menim meqsedlerim
    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.GetMyAsync(userId));
    }

    // meqsede pul elave et
    [HttpPost("{id}/contribute")]
    public async Task<IActionResult> Contribute(Guid id, ContributeGoalDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _service.ContributeAsync(userId, id, dto));
    }

    // meqseddan pul cixar
    [HttpPost("{id}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, ContributeGoalDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _service.WithdrawAsync(userId, id, dto) });
    }

    // meqsedi sil
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _service.DeleteAsync(userId, id) });
    }
}