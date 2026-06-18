using BankApiApp.DTOs.Loan;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LoanController : ControllerBase
{
    private readonly ILoanService _loanService;
    public LoanController(ILoanService loanService) => _loanService = loanService;

    [HttpPost("request")]
    public async Task<IActionResult> Request(LoanRequestDto dto)
        => Ok(await _loanService.RequestLoanAsync(GetUserId(), dto));

    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
        => Ok(await _loanService.GetMyLoansAsync(GetUserId()));

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPending()
    => Ok(await _loanService.GetPendingLoansAsync());

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Approve(Guid id)
        => Ok(await _loanService.ApproveLoanAsync(id));

    [HttpPost("{id}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reject(Guid id)
        => Ok(await _loanService.RejectLoanAsync(id));
}