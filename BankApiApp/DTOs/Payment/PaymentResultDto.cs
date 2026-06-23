namespace BankApiApp.DTOs.Payment;

public class PaymentResultDto
{
    public bool Success { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal NewBalance { get; set; }
    public string? Message { get; set; }
}