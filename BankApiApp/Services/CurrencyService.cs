using BankApiApp.DTOs.Currency;
using BankApiApp.Exceptions;

namespace BankApiApp.Services;

public class CurrencyService : ICurrencyService
{
    // Sabit mezenneler: 1 vahid valyuta = ne qeder AZN.
    // (Sonra kenar API elave edilende yalniz bu hisse deyisecek.)
    private static readonly Dictionary<string, (string Name, decimal RateToAzn)> _rates = new()
    {
        ["AZN"] = ("Azərbaycan manatı", 1.00m),
        ["USD"] = ("ABŞ dolları", 1.70m),
        ["EUR"] = ("Avro", 1.85m),
        ["TRY"] = ("Türk lirəsi", 0.05m),
    };

    public RatesDto GetRates()
    {
        return new RatesDto
        {
            BaseCurrency = "AZN",
            UpdatedAt = DateTime.UtcNow,
            Rates = _rates.Select(r => new RateDto
            {
                Code = r.Key,
                Name = r.Value.Name,
                RateToAzn = r.Value.RateToAzn
            }).ToList()
        };
    }

    public ConversionResultDto Convert(string from, string to, decimal amount)
    {
        // giris yoxlamalari
        if (amount <= 0)
            throw new BadRequestException("Məbləğ 0-dan böyük olmalıdır.");

        from = (from ?? "").Trim().ToUpperInvariant();
        to = (to ?? "").Trim().ToUpperInvariant();

        if (!_rates.ContainsKey(from))
            throw new BadRequestException($"Naməlum valyuta: {from}");
        if (!_rates.ContainsKey(to))
            throw new BadRequestException($"Naməlum valyuta: {to}");

        // Cevirme: once AZN-e, sonra hedefe
        // 1 From = RateToAzn[from] AZN
        // 1 To   = RateToAzn[to]   AZN
        // netice = amount * (rateFrom / rateTo)
        var rateFrom = _rates[from].RateToAzn;
        var rateTo = _rates[to].RateToAzn;

        var rate = rateFrom / rateTo;             // 1 From = ne qeder To
        var result = amount * rate;

        return new ConversionResultDto
        {
            Amount = amount,
            From = from,
            To = to,
            Rate = Math.Round(rate, 4),
            Result = Math.Round(result, 2),
            ConvertedAt = DateTime.UtcNow
        };
    }
}