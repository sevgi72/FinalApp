namespace BankApiApp.Helpers;

// Analitika kateqoriyalarini + emeliyyat tesvirlerini Accept-Language-e gore
// tercume edir (Yol B). DB-ye toxunulmur, yalniz cavabda tercume.
public static class CategoryLocalizer
{
    // ── Analitika kateqoriyalari (AnalyticsService.Categorize qaytarir) ──
    private static readonly Dictionary<string, Dictionary<string, string>> CategoryMap = new()
    {
        ["Kommunal"] = new() { ["az"] = "Kommunal", ["en"] = "Utilities", ["ru"] = "Коммунальные" },
        ["Round-up yigim"] = new() { ["az"] = "Round-up yığım", ["en"] = "Round-up savings", ["ru"] = "Округление" },
        ["Kredit odenisi"] = new() { ["az"] = "Kredit ödənişi", ["en"] = "Loan payment", ["ru"] = "Платёж по кредиту" },
        ["Nagd cixaris"] = new() { ["az"] = "Nağd çıxarış", ["en"] = "Cash withdrawal", ["ru"] = "Снятие наличных" },
        ["Kocurme"] = new() { ["az"] = "Köçürmə", ["en"] = "Transfer", ["ru"] = "Перевод" },
        ["Diger"] = new() { ["az"] = "Digər", ["en"] = "Other", ["ru"] = "Другое" },
    };

    // ── Emeliyyat tesvirleri (TransactionService DB-ye yazir) ──
    // ── Emeliyyat tesvirleri (TransactionService DB-ye yazir) ──
    private static readonly Dictionary<string, Dictionary<string, string>> DescriptionMap = new()
    {
        ["Deposit"] = new() { ["az"] = "Balans artımı", ["en"] = "Deposit", ["ru"] = "Пополнение" },
        ["Withdraw"] = new() { ["az"] = "Nağd çıxarış", ["en"] = "Withdrawal", ["ru"] = "Снятие" },
        ["Hesablar arasi kocurme"] = new() { ["az"] = "Hesablar arası köçürmə", ["en"] = "Between accounts", ["ru"] = "Между счетами" },
        ["Kocurme komissiyasi"] = new() { ["az"] = "Köçürmə komissiyası", ["en"] = "Transfer fee", ["ru"] = "Комиссия за перевод" },
        ["Kredit verildi"] = new() { ["az"] = "Kredit verildi", ["en"] = "Loan issued", ["ru"] = "Кредит выдан" },
        ["Kesbek balansi kocuruldu"] = new() { ["az"] = "Keşbek balansı köçürüldü", ["en"] = "Cashback transferred", ["ru"] = "Кэшбэк переведён" },
        ["Köçürmə"] = new() { ["az"] = "Köçürmə", ["en"] = "Transfer", ["ru"] = "Перевод" },
        ["Perevod"] = new() { ["az"] = "Köçürmə", ["en"] = "Transfer", ["ru"] = "Перевод" },
    };

    // Kateqoriyani secilen dile cevir. Taninmirsa - oldugu kimi qaytar.
    public static string Localize(string category, string lang)
    {
        lang = Normalize(lang);
        if (CategoryMap.TryGetValue(category, out var tr) && tr.TryGetValue(lang, out var val))
            return val;
        return category;
    }

    // Emeliyyat tesvirini secilen dile cevir. Taninmirsa - oldugu kimi qaytar
    // (ozel/serbest metnler - "Kirayə haqqı", "Meqsede yigim: ..." - deyisdirilmir).
    // ── Prefiksli tesvirler (sabit bas + deyisen sonluq) ──
    // Acar = AZ prefiks. Deger = her dilde prefiks. Sonluq (istifadeci metni) oldugu kimi qalir.
    private static readonly Dictionary<string, Dictionary<string, string>> PrefixMap = new()
    {
        ["Meqsede yigim:"] = new() { ["az"] = "Məqsədə yığım:", ["en"] = "Goal saving:", ["ru"] = "Накопление на цель:" },
    };

    // Emeliyyat tesvirini secilen dile cevir. Taninmirsa - oldugu kimi qaytar
    // (ozel/serbest metnler - "Kirayə haqqı" - deyisdirilmir).
    public static string LocalizeDescription(string? description, string lang)
    {
        if (string.IsNullOrEmpty(description)) return description ?? "";
        lang = Normalize(lang);

        // 1. Tam uygunluq (sabit tesvirler)
        if (DescriptionMap.TryGetValue(description, out var tr) && tr.TryGetValue(lang, out var val))
            return val;

        // 2. Prefiks uygunlugu (basi sabit, sonu istifadeci metni)
        foreach (var p in PrefixMap)
        {
            if (description.StartsWith(p.Key) && p.Value.TryGetValue(lang, out var prefix))
            {
                var rest = description.Substring(p.Key.Length);  // ": Telefon ucun" hissesi
                return prefix + rest;
            }
        }

        // 3. Taninmirsa - oldugu kimi
        return description;
    }

    // "en-US,en;q=0.9" kimi header-den ilk dili cixart. Default = az.
    public static string Normalize(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage)) return "az";
        var lang = acceptLanguage.Trim().ToLowerInvariant();
        if (lang.StartsWith("en")) return "en";
        if (lang.StartsWith("ru")) return "ru";
        return "az";
    }
}