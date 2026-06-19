using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class RefreshToken : AuditableEntity   // Id + CreatedAt buradan
{
    public string Token { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
}