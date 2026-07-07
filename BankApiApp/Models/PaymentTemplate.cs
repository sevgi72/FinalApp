using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class PaymentTemplate : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;          // "Evimin isigi"
    public Guid ProviderId { get; set; }
    public UtilityProvider Provider { get; set; } = null!;
    public string SubscriberCode { get; set; } = string.Empty;
}