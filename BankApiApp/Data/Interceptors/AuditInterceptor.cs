using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace BankApiApp.Data.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    // audit-ə YAZILMAYACAQ həssas sahələr (təhlükəsizlik)
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "Token",
        "Version"   // xmin concurrency token — mənasızdır audit üçün
    };

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // SaveChanges baş verməzdən ƏVVƏL araya gir
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context != null)
            AddAuditLogs(context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditLogs(DbContext context)
    {
        // cari istifadəçi + IP (HTTP sorğusundan)
        var httpContext = _httpContextAccessor.HttpContext;
        var userId = httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = httpContext?.User?.FindFirstValue(ClaimTypes.Email);
        var ip = httpContext?.Connection?.RemoteIpAddress?.ToString();
        var now = DateTime.UtcNow;

        var auditEntries = new List<AuditLog>();

        // ChangeTracker-də dəyişən bütün entity-lərə bax
        foreach (var entry in context.ChangeTracker.Entries())
        {
            // AuditLog-un özünü loglama (sonsuz dövr olmasın)
            if (entry.Entity is AuditLog)
                continue;

            // yalnız əlavə/dəyişmə/silmə (dəyişməyənləri keç)
            if (entry.State != EntityState.Added &&
                entry.State != EntityState.Modified &&
                entry.State != EntityState.Deleted)
                continue;

            var action = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Modified => "Modified",
                EntityState.Deleted => "Deleted",
                _ => "Unknown"
            };

            var changes = BuildChanges(entry);

            auditEntries.Add(new AuditLog
            {
                EntityName = entry.Entity.GetType().Name,
                Action = action,
                EntityId = TryGetEntityId(entry),
                Changes = changes,
                UserId = userId,
                UserEmail = userEmail,
                IpAddress = ip,
                CreatedAt = now
            });
        }

        // audit qeydlərini context-ə əlavə et (eyni SaveChanges-də yazılacaq)
        if (auditEntries.Count > 0)
            context.Set<AuditLog>().AddRange(auditEntries);
    }

    // dəyişən sahələri köhnə→yeni kimi JSON-a yığ (həssasları çıxararaq)
    private static string? BuildChanges(EntityEntry entry)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;

            // həssas sahələri atla
            if (SensitiveFields.Contains(name))
                continue;

            if (entry.State == EntityState.Added)
            {
                dict[name] = prop.CurrentValue;
            }
            else if (entry.State == EntityState.Deleted)
            {
                dict[name] = prop.OriginalValue;
            }
            else if (entry.State == EntityState.Modified && prop.IsModified)
            {
                // yalnız dəyişən sahələr: köhnə → yeni
                dict[name] = new { old = prop.OriginalValue, @new = prop.CurrentValue };
            }
        }

        if (dict.Count == 0)
            return null;

        return JsonSerializer.Serialize(dict, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    // entity-nin Id-sini tapmağa çalış
    private static string? TryGetEntityId(EntityEntry entry)
    {
        var idProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");
        return idProp?.CurrentValue?.ToString();
    }
}