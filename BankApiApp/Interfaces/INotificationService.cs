using BankApiApp.DTOs.Notification;

namespace BankApiApp.Services;

public interface INotificationService
{
    // daxili istifade - hadise bas verende bildiris gonder
    Task SendToUserAsync(string userId, string title, string message, string type = "Info");

    // admin - hamiya elan
    Task<string> BroadcastAsync(BroadcastNotificationDto dto);

    // istifadeci - oz bildirisleri
    Task<List<NotificationDto>> GetMyAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
    Task<string> MarkAsReadAsync(string userId, Guid id);
    Task<string> MarkAllReadAsync(string userId);
}