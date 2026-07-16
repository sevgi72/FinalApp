using BankApiApp.DTOs.Ai;

namespace BankApiApp.Services;

public interface IAiService
{
    Task<string> AskAsync(AskAiDto dto, string lang);
}