using BankApiApp.DTOs.Currency;

namespace BankApiApp.Services;

public interface ICurrencyService
{
    // Butun mezenneleri qaytarir (AZN-e gore)
    RatesDto GetRates();

    // from valyutasindan to valyutasina cevirir
    ConversionResultDto Convert(string from, string to, decimal amount);
}