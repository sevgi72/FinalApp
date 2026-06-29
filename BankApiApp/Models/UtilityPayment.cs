using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class UtilityPayment : AuditableEntity   // Id, CreatedAt, UpdatedAt
{
    public Guid ProviderId { get; set; }              // hansı provayder
    public UtilityProvider Provider { get; set; } = null!;
    public string SubscriberCode { get; set; } = string.Empty;   // abunəçi kodu (sayğac və s.)
    public decimal Amount { get; set; }               // ödənilən məbləğ
    public Guid AccountId { get; set; }               // hansı hesabdan
    public string Status { get; set; } = "Completed"; // ödəniş statusu
}