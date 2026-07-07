using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class SavingsGoal : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;          // "Telefon ucun"
    public decimal TargetAmount { get; set; }                 // hedef mebleg
    public decimal CurrentAmount { get; set; }                // toplanan mebleg
    public Guid SourceAccountId { get; set; }                 // hansi hesabdan yigilir
    public DateTime? Deadline { get; set; }                   // son tarix (isteye gore)
    public bool IsCompleted { get; set; }                     // hedefe catib?
}