using BankApiApp.DTOs.Admin;

namespace BankApiApp.Services;

public interface IAdminService
{
    Task<List<UserDto>> GetUsersAsync();
    Task AssignRoleAsync(string userId, string role);
    Task RemoveRoleAsync(string userId, string role);
    Task<string> BlockUserAsync(string callerUserId, string targetUserId);
    Task<string> UnblockUserAsync(string callerUserId, string targetUserId);
    Task<UserDetailDto> GetUserDetailAsync(string targetUserId);
}