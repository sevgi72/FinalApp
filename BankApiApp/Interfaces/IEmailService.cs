namespace BankApiApp.Services;

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody);
}