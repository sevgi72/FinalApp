namespace BankApiApp.DTOs.Transaction;

public class TransactionDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? FromAccountId { get; set; }
    public Guid? ToAccountId { get; set; }
    public DateTime CreatedAt { get; set; }
}