using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class CashByCode : AuditableEntity
{
    public string Code { get; set; } = string.Empty;        // 8 reqemli kod
    public decimal Amount { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public Guid SenderAccountId { get; set; }               // pul burdan cixib (legv olsa bura qayidir)
    public string Status { get; set; } = "Pending";         // Pending / Claimed / Cancelled / Expired
    public DateTime ExpiresAt { get; set; }                 // vaxt limiti (72 saat)
    public string? ClaimedByUserId { get; set; }            // kim goturdu
    public DateTime? ClaimedAt { get; set; }
}