using System.Text.Json;

namespace BankApiApp.Services;

public class LocalizationService : ILocalizationService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    // bütün dillərin tərcümələri yaddaşda saxlanır (bir dəfə oxunur)
    private static readonly Dictionary<string, Dictionary<string, string>> _translations = new();
    private static readonly string[] SupportedLanguages = { "az", "en", "ru" };
    private const string DefaultLanguage = "az";

    static LocalizationService()
    {
        // app başlayanda JSON fayllarını bir dəfə oxu
        foreach (var lang in SupportedLanguages)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", $"{lang}.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _translations[lang] = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
        }
    }

    public LocalizationService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string Get(string key)
    {
        // istifadəçinin dilini Accept-Language header-dən oxu
        var lang = GetRequestLanguage();

        // tərcümə varsa qaytar, yoxsa default dildə yoxla, o da yoxsa açarın özünü qaytar
        if (_translations.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var value))
            return value;

        if (_translations.TryGetValue(DefaultLanguage, out var defaultDict) && defaultDict.TryGetValue(key, out var defaultValue))
            return defaultValue;

        return key;
    }

    // Accept-Language header-dən dili çıxar (məs. "ru", "en-US" → "en")
    private string GetRequestLanguage()
    {
        var header = _httpContextAccessor.HttpContext?.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrEmpty(header))
            return DefaultLanguage;

        // header "en-US,en;q=0.9" kimi ola bilər — ilk 2 hərfi götür
        var lang = header.Split(',')[0].Trim().ToLower();
        if (lang.Length > 2) lang = lang[..2];

        return SupportedLanguages.Contains(lang) ? lang : DefaultLanguage;
    }
}