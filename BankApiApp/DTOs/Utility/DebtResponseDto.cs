namespace BankApiApp.DTOs.Utility;

public class DebtResponseDto
{
    public string ProviderName { get; set; } = string.Empty;
    public string SubscriberCode { get; set; } = string.Empty;
    public decimal DebtAmount { get; set; }
}