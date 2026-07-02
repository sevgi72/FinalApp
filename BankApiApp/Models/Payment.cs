using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class Payment : AuditableEntity   // Id + CreatedAt
{
    public Guid AccountId { get; set; }            // pul hansi hesaba yuklendi
    public Account Account { get; set; } = null!;

    public decimal Amount { get; set; }
    public string CardLast4 { get; set; } = string.Empty;   // YALNIZ son 4 reqem
    public PaymentStatus Status { get; set; }
    public string Reference { get; set; } = string.Empty;   // unikal odenis kodu
    public string? FailureReason { get; set; }              // ugursuzsa sebeb
}