namespace BankApiApp.DTOs.Utility;

public class UtilityPaymentDto
{
    public Guid Id { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SubscriberCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}