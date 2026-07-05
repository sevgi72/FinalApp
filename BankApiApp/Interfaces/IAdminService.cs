using BankApiApp.DTOs.Admin;
using BankApiApp.DTOs.Common;

namespace BankApiApp.Services;

public interface IAdminService
{
    Task<PagedResult<UserDto>> GetUsersAsync(PaginationParams pagination, string? search, bool? blocked);
    Task AssignRoleAsync(string userId, string role);
    Task RemoveRoleAsync(string userId, string role);
    Task<string> BlockUserAsync(string callerUserId, string targetUserId);
    Task<string> UnblockUserAsync(string callerUserId, string targetUserId);
    Task<UserDetailDto> GetUserDetailAsync(string targetUserId);
    Task<AdminStatsDto> GetStatsAsync();
}