namespace BankApiApp.DTOs.Transactions;

public class DepositDto
{
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
}