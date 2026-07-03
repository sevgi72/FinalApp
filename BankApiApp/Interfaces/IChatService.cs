using BankApiApp.DTOs.Chat;

namespace BankApiApp.Services;

public interface IChatService
{
    // istifadəçi öz söhbətini görür
    Task<List<ChatMessageDto>> GetMyMessagesAsync(string userId);

    // admin konkret istifadəçi ilə söhbəti görür
    Task<List<ChatMessageDto>> GetConversationAsync(string userId);

    // admin bütün söhbətlərin siyahısını görür
    Task<List<ChatConversationDto>> GetConversationsAsync();
}