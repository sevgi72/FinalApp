namespace BankApiApp.DTOs.Card;

// Kart parametrlerini yenilemek ucun (hamisi opsional — yalniz gonderilen deyisir)
public class UpdateCardSettingsDto
{
    public string? CardName { get; set; }
    public decimal? MonthlyLimit { get; set; }
    public bool? OnlinePaymentsEnabled { get; set; }
}