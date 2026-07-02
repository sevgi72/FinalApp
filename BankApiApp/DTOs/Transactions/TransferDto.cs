namespace BankApiApp.DTOs.Transactions;

public class TransferDto
{
    public Guid FromAccountId { get; set; }        // menim hesabim
    public string ToAccountNumber { get; set; } = string.Empty;  // alanin hesab nomresi
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}