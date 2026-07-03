namespace BankApiApp.DTOs.Chat;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsFromAdmin { get; set; }
    public DateTime SentAt { get; set; }
}