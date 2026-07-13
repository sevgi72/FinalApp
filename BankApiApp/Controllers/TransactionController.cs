using BankApiApp.DTOs.Common;
using BankApiApp.DTOs.Transaction;
using BankApiApp.DTOs.Transactions;
using BankApiApp.Helpers;
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
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _transactionService.DepositAsync(userId, dto));
    }

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw(WithdrawDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _transactionService.WithdrawAsync(userId, dto));
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer(TransferDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _transactionService.TransferAsync(userId, dto));
    }

    [HttpGet("account/{accountId}")]
    public async Task<IActionResult> GetAccountTransactions(
        Guid accountId, [FromQuery] PaginationParams pagination)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _transactionService.GetAccountTransactionsAsync(userId, accountId, pagination);

        // Accept-Language-e gore emeliyyat tesvirlerini tercume et (Yol B).
        // Taninan sabit tesvirler tercume olunur, serbest metnler (Kirayə haqqı) qalir.
        var lang = CategoryLocalizer.Normalize(Request.Headers["Accept-Language"].ToString());
        foreach (var item in result.Items)
            item.Description = CategoryLocalizer.LocalizeDescription(item.Description, lang);

        return Ok(result);
    }

    [HttpPost("transfer-by-card")]
    public async Task<IActionResult> TransferByCard(TransferByCardDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _transactionService.TransferByCardAsync(userId, dto);
        return Ok(new { message = "Kocurme ugurla tamamlandi." });
    }

    [HttpGet("{id}/receipt")]
    public async Task<IActionResult> GetReceipt(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var receipt = await _transactionService.GetReceiptAsync(userId, id);

        // qebzdeki tesviri de tercume et
        var lang = CategoryLocalizer.Normalize(Request.Headers["Accept-Language"].ToString());
        receipt.Description = CategoryLocalizer.LocalizeDescription(receipt.Description, lang);

        return Ok(receipt);
    }

    [HttpGet("{id}/receipt/pdf")]
    public async Task<IActionResult> GetReceiptPdf(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var (pdf, fileName) = await _transactionService.GetReceiptPdfAsync(userId, id);
        return File(pdf, "application/pdf", fileName);
    }

    // oz hesablari arasi transfer
    [HttpPost("self-transfer")]
    public async Task<IActionResult> SelfTransfer(SelfTransferDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _transactionService.SelfTransferAsync(userId, dto));
    }
}