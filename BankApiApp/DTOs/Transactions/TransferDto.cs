namespace BankApiApp.DTOs.Transactions;

public class TransferDto
{
    public Guid FromAccountId { get; set; }        // mənim hesabım
    public string ToAccountNumber { get; set; } = string.Empty;  // alanın hesab nömrəsi
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}