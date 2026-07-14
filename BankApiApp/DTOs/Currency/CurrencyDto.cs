namespace BankApiApp.DTOs.Currency;

// Tek valyutanin AZN-e gore mezennesi (siyahi ucun)
public class RateDto
{
    public string Code { get; set; } = "";       // USD, EUR, TRY, AZN
    public string Name { get; set; } = "";        // ABŞ dolları, Avro...
    public decimal RateToAzn { get; set; }        // 1 vahid = ne qeder AZN
}

// Butun mezennelerin siyahisi
public class RatesDto
{
    public string BaseCurrency { get; set; } = "AZN";
    public DateTime UpdatedAt { get; set; }
    public List<RateDto> Rates { get; set; } = new();
}

// Cevirme neticesi
public class ConversionResultDto
{
    public decimal Amount { get; set; }           // giris meblegi
    public string From { get; set; } = "";        // menbe valyuta
    public string To { get; set; } = "";          // hedef valyuta
    public decimal Rate { get; set; }             // 1 From = ne qeder To
    public decimal Result { get; set; }           // netice meblegi
    public DateTime ConvertedAt { get; set; }
}