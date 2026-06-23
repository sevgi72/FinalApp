namespace BankApiApp.DTOs.Payment;

public class TopUpDto
{
    public Guid AccountId { get; set; }       // hansı hesaba
    public string CardNumber { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }       // tam il, məs. 2030
    public string Cvv { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}