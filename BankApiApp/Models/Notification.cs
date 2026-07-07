using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class Notification : BaseEntity   // Id
{
    public string UserId { get; set; } = string.Empty;    // kime (bos = hamiya elan)
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "Info";            // Info / Transaction / Promo / System
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}