using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Deposit;

public class CreateDepositDto
{
    [Required(ErrorMessage = "Hesab secilmelidir.")]
    public Guid SourceAccountId { get; set; }

    [Range(10, double.MaxValue, ErrorMessage = "Minimum emanet 10 AZN-dir.")]
    public decimal Amount { get; set; }
}

public class DepositDto
{
    public Guid Id { get; set; }
    public decimal Principal { get; set; }
    public decimal EarnedInterest { get; set; }
    public decimal TotalValue { get; set; }        // principal + faiz
    public decimal AnnualRate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastInterestAt { get; set; }
}