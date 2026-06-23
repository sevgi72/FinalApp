namespace BankApiApp.DTOs.Card;

public class CardCreatedDto
{
    public Guid Id { get; set; }
    public string CardNumber { get; set; } = string.Empty;   // tam nömrə (yalnız indi)
    public string Cvv { get; set; } = string.Empty;          // yalnız indi, saxlanmır
    public string CardHolderName { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}