using BankApiApp.DTOs.Audit;
using BankApiApp.DTOs.Common;

namespace BankApiApp.Services;

public interface IAuditService
{
    Task LogManualAsync(string action, string? details = null, string? userId = null, string? userEmail = null);
    Task<PagedResult<AuditLogDto>> GetLogsAsync(PaginationParams pagination);
}