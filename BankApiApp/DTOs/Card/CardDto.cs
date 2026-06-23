namespace BankApiApp.DTOs.Card;

public class CardDto
{
    public Guid Id { get; set; }
    public string MaskedNumber { get; set; } = string.Empty;
    public string CardHolderName { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;   // "06/30"
    public string Status { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
}