using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Notification;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BroadcastNotificationDto
{
    [Required(ErrorMessage = "Baslq mutleqdir.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mesaj mutleqdir.")]
    public string Message { get; set; } = string.Empty;
}