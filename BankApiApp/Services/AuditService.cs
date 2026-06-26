using BankApiApp.Data;
using BankApiApp.DTOs.Audit;
using BankApiApp.DTOs.Common;
using BankApiApp.Extensions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BankApiApp.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    // əl ilə audit (login, uğursuz giriş kimi bazaya yazılmayan hadisələr üçün)
    public async Task LogManualAsync(
        string action, string? details = null, string? userId = null, string? userEmail = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var ip = httpContext?.Connection?.RemoteIpAddress?.ToString();

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = "Auth",        // əl ilə hadisələr üçün
            Action = action,            // "Login", "LoginFailed", "PasswordChanged"...
            Changes = details,          // əlavə açıqlama
            UserId = userId,
            UserEmail = userEmail,
            IpAddress = ip,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    public async Task<PagedResult<AuditLogDto>> GetLogsAsync(PaginationParams pagination)
    {
        var query = _context.AuditLogs
            .OrderByDescending(a => a.CreatedAt);

        var paged = await query.ToPagedResultAsync(pagination);

        return new PagedResult<AuditLogDto>
        {
            Items = paged.Items.Select(a => new AuditLogDto
            {
                Id = a.Id,
                EntityName = a.EntityName,
                Action = a.Action,
                EntityId = a.EntityId,
                Changes = a.Changes,
                UserId = a.UserId,
                UserEmail = a.UserEmail,
                IpAddress = a.IpAddress,
                CreatedAt = a.CreatedAt
            }).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages
        };
    }
}