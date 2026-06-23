namespace BankApiApp.DTOs.Loan;

public class LoanPaymentDto
{
    public int PaymentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
}