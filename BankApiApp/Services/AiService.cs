using System.Text;
using System.Text.Json;
using BankApiApp.DTOs.Ai;
using BankApiApp.Exceptions;

namespace BankApiApp.Services;

public class AiService : IAiService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    // DÜZƏLİŞ 1: Düzgün rəsmi model adı
    private const string Model = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

    public AiService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    private static string BuildSystemPrompt(string lang) => $@"
Sen Amorabank onlayn bank tetbiqinin komekcisisen.
Tetbiqde: hesablar, cixaris (PDF), kocurmeler (IBAN/kart), kartlar (limit/blok),
kommunal odenisler, yigim meqsedleri, round-up, kredit, kesbek, analitika,
balans xeberdarligi, 2FA.
Qaydalar: QISA cavab ver (max 4 cumle). Dil: {LangName(lang)}.
Yalniz bank movzusunda cavab ver. Istifadecinin balansini bilmirsen.
Investisiya mesleheti verme.";

    private static string LangName(string lang) => lang switch
    {
        "en" => "English",
        "ru" => "Russian (Русский)",
        _ => "Azerbaijani (Azerbaycan dili)"
    };

    public async Task<string> AskAsync(AskAiDto dto, string lang)
    {
        var apiKey = _config["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new BadRequestException("AI xidmeti konfiqurasiya olunmayib.");

        var question = dto.Question?.Trim();
        if (string.IsNullOrEmpty(question))
            throw new BadRequestException("Sual bos ola bilmez.");

        // DÜZƏLİŞ 2: thinkingConfig silindi (standart modellərdə 400 xətası verir)
        var payload = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = BuildSystemPrompt(lang) } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = question } }
                }
            },
            generationConfig = new
            {
                maxOutputTokens = 600
            }
        };

        var json = JsonSerializer.Serialize(payload);

        HttpResponseMessage response = null!;
        string body = "";

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Post, $"{BaseUrl}{Model}:generateContent");
            req.Headers.Add("x-goog-api-key", apiKey);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                response = await _http.SendAsync(req);
            }
            catch (Exception)
            {
                throw new BadRequestException("AI xidmetine qosulmaq mumkun olmadi.");
            }

            body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode) break;

            if ((int)response.StatusCode != 503 || attempt == 3) break;

            await Task.Delay(800 * attempt);
        }

        if (!response.IsSuccessStatusCode)
        {
            var code = (int)response.StatusCode;
            if (code == 429)
                throw new BadRequestException("AI limiti doldu. 1 deqiqe gozleyib yeniden cehd edin.");
            if (code == 503)
                throw new BadRequestException("AI komekci hazirda cox yuklüdür. Bir azdan cehd edin.");

            // Xətanın dəqiq nə olduğunu konsolda/response-da görmək üçün body-dən də istifadə edə bilərsən
            throw new BadRequestException($"AI xidmeti xeta qaytardi ({code}).");
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return string.IsNullOrWhiteSpace(text)
                ? "Cavab alina bilmedi. Yeniden cehd edin."
                : text.Trim();
        }
        catch
        {
            throw new BadRequestException("AI cavab vere bilmedi. Sualinizi basqa cur yazin.");
        }
    }
}