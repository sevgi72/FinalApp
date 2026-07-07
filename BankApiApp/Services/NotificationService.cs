using BankApiApp.Data;
using BankApiApp.DTOs.Notification;
using BankApiApp.Exceptions;
using BankApiApp.Hubs;
using BankApiApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationService(AppDbContext context,
        IHubContext<NotificationHub> hub,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _hub = hub;
        _userManager = userManager;
    }

    // konkret istifadeciye bildiris (baza + canli itele)
    public async Task SendToUserAsync(string userId, string title, string message, string type = "Info")
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            IsRead = false
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // canli itele (istifadeci onlayndirsa derhal gorur)
        await _hub.Clients.User(userId).SendAsync("ReceiveNotification", new
        {
            id = notification.Id,
            title = notification.Title,
            message = notification.Message,
            type = notification.Type,
            createdAt = notification.CreatedAt
        });
    }

    // admin - butun istifadecilere elan
    public async Task<string> BroadcastAsync(BroadcastNotificationDto dto)
    {
        var userIds = await _userManager.Users.Select(u => u.Id).ToListAsync();

        var notifications = userIds.Select(uid => new Notification
        {
            UserId = uid,
            Title = dto.Title,
            Message = dto.Message,
            Type = "Promo",
            IsRead = false
        }).ToList();

        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();

        // hamiya canli itele
        await _hub.Clients.All.SendAsync("ReceiveNotification", new
        {
            title = dto.Title,
            message = dto.Message,
            type = "Promo",
            createdAt = DateTime.UtcNow
        });

        return $"{userIds.Count} istifadeciye elan gonderildi.";
    }

    public async Task<List<NotificationDto>> GetMyAsync(string userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<string> MarkAsReadAsync(string userId, Guid id)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId)
            ?? throw new NotFoundException("Bildiris tapilmadi.");

        notification.IsRead = true;
        await _context.SaveChangesAsync();

        return "Bildiris oxunmus kimi isarelendi.";
    }

    public async Task<string> MarkAllReadAsync(string userId)
    {
        var unread = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
            n.IsRead = true;

        await _context.SaveChangesAsync();

        return $"{unread.Count} bildiris oxunmus kimi isarelendi.";
    }
}