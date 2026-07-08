using BankApiApp.DTOs.Admin;
using BankApiApp.DTOs.Commission;
using BankApiApp.DTOs.Common;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]   // butun endpoint-ler yalniz admin ucun
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ICommissionService _commissionService;

    public AdminController(IAdminService adminService, ICommissionService commissionService)
    {
        _adminService = adminService;
        _commissionService = commissionService;
    }

    // istifadeci siyahisi (axtaris + blok filtri)
    [HttpGet("users")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] PaginationParams pagination,
        [FromQuery] string? search,
        [FromQuery] bool? blocked)
        => Ok(await _adminService.GetUsersAsync(pagination, search, blocked));

    [HttpPost("users/{userId}/roles")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> AssignRole(string userId, AssignRoleDto dto)
    {
        await _adminService.AssignRoleAsync(userId, dto.Role);
        return Ok(new { message = "Rol teyin edildi." });
    }

    [HttpDelete("users/{userId}/roles/{role}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> RemoveRole(string userId, string role)
    {
        await _adminService.RemoveRoleAsync(userId, role);
        return Ok(new { message = "Rol silindi." });
    }

    // istifadecini blokla
    [HttpPost("users/{userId}/block")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> BlockUser(string userId)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _adminService.BlockUserAsync(callerId, userId) });
    }

    // bloku ac
    [HttpPost("users/{userId}/unblock")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> UnblockUser(string userId)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _adminService.UnblockUserAsync(callerId, userId) });
    }

    // istifadeci detallari
    [HttpGet("users/{userId}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> GetUserDetail(string userId)
        => Ok(await _adminService.GetUserDetailAsync(userId));

    // bank statistikasi (dashboard)
    [HttpGet("stats")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> GetStats()
        => Ok(await _adminService.GetStatsAsync());

    // hesabi dondur (subheli fealiyyet)
    [HttpPost("accounts/{accountId}/freeze")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> FreezeAccount(Guid accountId, [FromQuery] string? reason)
        => Ok(new { message = await _adminService.FreezeAccountAsync(accountId, reason) });

    // hesabi geri ac
    [HttpPost("accounts/{accountId}/unfreeze")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> UnfreezeAccount(Guid accountId)
        => Ok(new { message = await _adminService.UnfreezeAccountAsync(accountId) });

    // komissiya qaydalari (admin gorur)
    [HttpGet("commission-rules")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> GetCommissionRules()
        => Ok(await _commissionService.GetRulesAsync());

    // komissiya qaydasi yenile (yalniz SuperAdmin)
    [HttpPut("commission-rules/{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> UpdateCommissionRule(Guid id, UpdateCommissionRuleDto dto)
        => Ok(new { message = await _commissionService.UpdateRuleAsync(id, dto) });
}