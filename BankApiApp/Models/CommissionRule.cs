using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class CommissionRule : AuditableEntity
{
    public string TransactionType { get; set; } = "Transfer";   // hansi emeliyyat
    public decimal MinAmount { get; set; }                      // bu meblegden yuxari komissiya tutulur
    public decimal Percentage { get; set; }                     // komissiya faizi 
    public decimal MaxCommission { get; set; }                
    public bool IsActive { get; set; } = true;
}