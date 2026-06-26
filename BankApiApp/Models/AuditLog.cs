using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class AuditLog : BaseEntity   // Id buradan
{
    public string EntityName { get; set; } = string.Empty;   // hansı cədvəl (Account, Card...)
    public string Action { get; set; } = string.Empty;       // Created / Modified / Deleted
    public string? EntityId { get; set; }                    // dəyişən sətrin id-si
    public string? Changes { get; set; }                     // köhnə→yeni dəyərlər (JSON)
    public string? UserId { get; set; }                      // kim
    public string? UserEmail { get; set; }
    public string? IpAddress { get; set; }                   // haradan
    public DateTime CreatedAt { get; set; }                  // nə vaxt
}