namespace BankApiApp.DTOs.Transactions;

public class TransferByCardDto
{
    public Guid FromAccountId { get; set; }       // göndərənin hesabı
    public string ToCardNumber { get; set; } = string.Empty;   // alıcının kart nömrəsi
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}