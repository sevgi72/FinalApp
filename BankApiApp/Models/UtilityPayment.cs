using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class UtilityPayment : AuditableEntity   // Id, CreatedAt, UpdatedAt
{
    public Guid ProviderId { get; set; }              // hansi provayder
    public UtilityProvider Provider { get; set; } = null!;
    public string SubscriberCode { get; set; } = string.Empty;   // abuneci kodu (saygac ve s.)
    public decimal Amount { get; set; }               // odenilen mebleg
    public Guid AccountId { get; set; }               // hansi hesabdan
    public string Status { get; set; } = "Completed"; // odenis statusu
}