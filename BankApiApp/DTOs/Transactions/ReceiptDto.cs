namespace BankApiApp.DTOs.Transactions;

public class ReceiptDto
{
    public string Reference { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? FromAccount { get; set; }   // maskalı hesab nömrəsi
    public string? ToAccount { get; set; }
    public string? Description { get; set; }
}