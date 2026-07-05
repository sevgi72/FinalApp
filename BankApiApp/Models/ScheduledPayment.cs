using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class ScheduledPayment : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public Guid AccountId { get; set; }                     // hansi hesabdan
    public Guid ProviderId { get; set; }                    // hansi provayder
    public UtilityProvider Provider { get; set; } = null!;
    public string SubscriberCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }                     // her defe odenilecek mebleg
    public int DayOfMonth { get; set; }                     // ayin neci gunu (1-28)
    public bool IsActive { get; set; } = true;
    public DateTime? LastExecutedAt { get; set; }           // sonuncu icra (tekrar olmasin)
}