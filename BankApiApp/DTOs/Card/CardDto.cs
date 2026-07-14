namespace BankApiApp.DTOs.Card;

public class CardDto
{
    public Guid Id { get; set; }
    public string MaskedNumber { get; set; } = string.Empty;
    public string FullNumber { get; set; } = string.Empty;   // tam nomre (goz duymesi ucun)
    public string CardHolderName { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;   // "06/30"
    public string Status { get; set; } = string.Empty;
    public Guid AccountId { get; set; }

    public string? CardName { get; set; }
    public decimal MonthlyLimit { get; set; }
    public bool OnlinePaymentsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }   // yaradilma tarixi
}