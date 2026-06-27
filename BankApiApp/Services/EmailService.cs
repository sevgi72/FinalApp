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
        // SMTP client qur (Gmail: smtp.gmail.com:587, TLS)
        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = true,   // TLS şifrələmə
            Credentials = new NetworkCredential(_settings.SenderEmail, _settings.Password)
        };

        // mesajı qur
        using var message = new MailMessage
        {
            From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true   // HTML məzmun (gözəl email üçün)
        };
        message.To.Add(toEmail);

        // göndər
        await client.SendMailAsync(message);
    }
}