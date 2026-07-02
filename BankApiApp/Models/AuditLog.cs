using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class AuditLog : BaseEntity   // Id buradan
{
    public string EntityName { get; set; } = string.Empty;   // hansi cedvel (Account, Card...)
    public string Action { get; set; } = string.Empty;       // Created / Modified / Deleted
    public string? EntityId { get; set; }                    // deyisen setrin id-si
    public string? Changes { get; set; }                     // kohne→yeni deyerler (JSON)
    public string? UserId { get; set; }                      // kim
    public string? UserEmail { get; set; }
    public string? IpAddress { get; set; }                   // haradan
    public DateTime CreatedAt { get; set; }                  // ne vaxt
}