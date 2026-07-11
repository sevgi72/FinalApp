using BankApiApp.Models.Common;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace BankApiApp.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        // ═══ DEVELOPMENT: SMTP evezine konsola yaz ═══
        // Real deploy-da asagidaki SMTP bloku aktivlesdirilir.
        // Hazirda tesdiq kodlari birbasa konsolda gorunur.
        Console.WriteLine();
        Console.WriteLine("╔═══════════════════ EMAIL ═══════════════════╗");
        Console.WriteLine($"  Kime:   {toEmail}");
        Console.WriteLine($"  Movzu:  {subject}");
        Console.WriteLine($"  Metn:   {StripHtml(htmlBody)}");
        Console.WriteLine("╚═════════════════════════════════════════════╝");
        Console.WriteLine();
        await Task.CompletedTask;

        /* ═══ REAL SMTP (deploy ucun — hazirda sondurulub) ═══
        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort);
        client.UseDefaultCredentials = false;
        client.Credentials = new NetworkCredential(_settings.SenderEmail, _settings.Password);
        client.EnableSsl = true;
        using var message = new MailMessage
        {
            From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);
        await client.SendMailAsync(message);
        */
    }

    // HTML teqlerini cixarib metni oxunaqli edir (konsol ucun)
    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ").Replace("&nbsp;", " ").Trim();
    }
}