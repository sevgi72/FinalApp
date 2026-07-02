namespace BankApiApp.DTOs.Transactions;

public class TransferByCardDto
{
    public Guid FromAccountId { get; set; }       // gonderenin hesabi
    public string ToCardNumber { get; set; } = string.Empty;   // alicinin kart nomresi
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}