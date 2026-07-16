using System.Text;
using System.Text.Json;
using BankApiApp.DTOs.Ai;
using BankApiApp.Exceptions;

namespace BankApiApp.Services;

public class AiService : IAiService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    // Model adi — Google modelleri yenileyende burani deyisin kifayetdir
    private const string Model = "gemini-2.0-flash-lite";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

    public AiService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    // Modele "kim oldugunu" izah edir — cavablar bank movzusunda qalir
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

        // Gemini sorgu govdesi
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
            generationConfig = new { maxOutputTokens = 600 }
        };

        var json = JsonSerializer.Serialize(payload);

        HttpResponseMessage response = null!;
        string body = "";

        // 503 (model yuklu) ve ya 429 halinda 3 defe cehd et — qisa gozleme ile
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

            // Ugurlu cavab aldiqsa, donguden cix
            if (response.IsSuccessStatusCode) break;

            // Model yuklu olanda (503) 3 defe cehd edirik.
            // 429 (kvota) halinda tekrar cehd VEZIYYETI PISLESDIRIR — derhal dayaniriq.
            if ((int)response.StatusCode != 503 || attempt == 3) break;

            // Cehd sayina gore gozleme muddeti: 1-ci cehdde 800ms, 2-ci cehdde 1600ms gozleyir
            await Task.Delay(800 * attempt);
        }

        // Əgər butun cehdlerden sonra da cavab ugurlu deyilse
        if (!response.IsSuccessStatusCode)
        {
            var code = (int)response.StatusCode;
            if (code == 429)
                throw new BadRequestException("AI limiti doldu. 1 deqiqe gozleyib yeniden cehd edin.");
            if (code == 503)
                throw new BadRequestException("AI komekci hazirda cox yuklüdür. Bir azdan cehd edin.");
            throw new BadRequestException($"AI xidmeti xeta qaytardi ({code}).");
        }

        // Cavabi cixar: candidates[0].content.parts[0].text
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
            // Model cavabi blok edibse ve ya format gozlenilmezdirse
            throw new BadRequestException("AI cavab vere bilmedi. Sualinizi basqa cur yazin.");
        }
    }
}