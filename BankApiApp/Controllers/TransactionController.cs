using BankApiApp.DTOs.Transaction;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    public TransactionController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit(DepositDto dto)
        => Ok(await _transactionService.DepositAsync(GetUserId(), dto));

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw(WithdrawDto dto)
        => Ok(await _transactionService.WithdrawAsync(GetUserId(), dto));

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer(TransferDto dto)
        => Ok(await _transactionService.TransferAsync(GetUserId(), dto));

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}