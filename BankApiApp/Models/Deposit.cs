using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class Deposit : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public Guid SourceAccountId { get; set; }               // hansi hesabdan acildi (bagl ananda bura qayidir)
    public decimal Principal { get; set; }                  // esas yatirilan mebleg
    public decimal EarnedInterest { get; set; }             // yigilan faiz
    public decimal AnnualRate { get; set; }                 // illik faiz derecesi (mes 0.12 = 12%)
    public bool IsActive { get; set; } = true;
    public DateTime? LastInterestAt { get; set; }           // sonuncu faiz hesablanma tarixi
}