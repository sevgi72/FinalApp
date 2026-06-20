using BankApiApp.DTOs.Admin;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]   // bütün endpoint-lər yalnız admin üçün
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
        => Ok(await _adminService.GetUsersAsync());

    [HttpPost("users/{userId}/roles")]
    public async Task<IActionResult> AssignRole(string userId, AssignRoleDto dto)
    {
        await _adminService.AssignRoleAsync(userId, dto.Role);
        return Ok(new { message = "Rol təyin edildi." });
    }

    [HttpDelete("users/{userId}/roles/{role}")]
    public async Task<IActionResult> RemoveRole(string userId, string role)
    {
        await _adminService.RemoveRoleAsync(userId, role);
        return Ok(new { message = "Rol silindi." });
    }
}