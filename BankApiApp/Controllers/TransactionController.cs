using BankApiApp.DTOs.Transaction;
using BankApiApp.DTOs.Transactions;
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


    [HttpGet("account/{accountId}")]
    public async Task<IActionResult> GetAccountTransactions(Guid accountId)
    => Ok(await _transactionService.GetAccountTransactionsAsync(GetUserId(), accountId));

    [HttpPost("transfer-by-card")]
    public async Task<IActionResult> TransferByCard(TransferByCardDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _transactionService.TransferByCardAsync(userId, dto);
        return Ok(new { message = "Köçürmə uğurla tamamlandı." });
    }

    [HttpGet("{id}/receipt")]
    public async Task<IActionResult> GetReceipt(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _transactionService.GetReceiptAsync(userId, id));
    }

    [HttpGet("{id}/receipt/pdf")]
    public async Task<IActionResult> GetReceiptPdf(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var (pdf, fileName) = await _transactionService.GetReceiptPdfAsync(userId, id);
        return File(pdf, "application/pdf", fileName);   // brauzer faylı endirir
    }
}