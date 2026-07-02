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

    // audit-e YAZILMAYACAQ hessas saheler (tehlukesizlik)
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "Token",
        "Version"   // xmin concurrency token — menasizdir audit ucun
    };

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // SaveChanges bas vermezden EVVEL araya gir
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
        // cari istifadeci + IP (HTTP sorgusundan)
        var httpContext = _httpContextAccessor.HttpContext;
        var userId = httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = httpContext?.User?.FindFirstValue(ClaimTypes.Email);
        var ip = httpContext?.Connection?.RemoteIpAddress?.ToString();
        var now = DateTime.UtcNow;

        var auditEntries = new List<AuditLog>();

        // ChangeTracker-de deyisen butun entity-lere bax
        foreach (var entry in context.ChangeTracker.Entries())
        {
            // AuditLog-un ozunu loglama (sonsuz dovr olmasin)
            if (entry.Entity is AuditLog)
                continue;

            // yalniz elave/deyisme/silme (deyismeyenleri kec)
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

        // audit qeydlerini context-e elave et (eyni SaveChanges-de yazilacaq)
        if (auditEntries.Count > 0)
            context.Set<AuditLog>().AddRange(auditEntries);
    }

    // deyisen saheleri kohne→yeni kimi JSON-a yig (hessaslari cixararaq)
    private static string? BuildChanges(EntityEntry entry)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;

            // hessas saheleri atla
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
                // yalniz deyisen saheler: kohne → yeni
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

    // entity-nin Id-sini tapmaga calis
    private static string? TryGetEntityId(EntityEntry entry)
    {
        var idProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");
        return idProp?.CurrentValue?.ToString();
    }
}