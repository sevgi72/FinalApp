namespace BankApiApp.DTOs.Chat;

public class ChatConversationDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public int UnreadCount { get; set; }   // oxunmamış mesaj sayı
}