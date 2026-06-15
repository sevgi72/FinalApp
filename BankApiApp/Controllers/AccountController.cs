using BankApiApp.DTOs.Account;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]   // bütün endpoint-lər token tələb edir
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAccountDto dto)
    {
        var result = await _accountService.CreateAccountAsync(GetUserId(), dto);
        return Ok(result);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyAccounts()
    {
        var result = await _accountService.GetMyAccountsAsync(GetUserId());
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _accountService.GetByIdAsync(GetUserId(), id);
        return Ok(result);
    }

    // token-dən cari istifadəçinin Id-sini çıxarır
    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}