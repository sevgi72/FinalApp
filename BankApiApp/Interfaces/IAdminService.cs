using BankApiApp.DTOs.Admin;

namespace BankApiApp.Services;

public interface IAdminService
{
    Task<List<UserDto>> GetUsersAsync();
    Task AssignRoleAsync(string userId, string role);
    Task RemoveRoleAsync(string userId, string role);
}