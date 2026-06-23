namespace BankApiApp.DTOs.Loan;

public class LoanDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public decimal Principal { get; set; }
    public decimal InterestRate { get; set; }
    public int TermMonths { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal TotalPayable { get; set; }
    public decimal RemainingBalance { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<LoanPaymentDto> Payments { get; set; } = new();
}