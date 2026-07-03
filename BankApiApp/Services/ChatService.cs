using BankApiApp.Data;
using BankApiApp.DTOs.Chat;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class ChatService : IChatService
{
    private readonly AppDbContext _context;

    public ChatService(AppDbContext context)
    {
        _context = context;
    }

    // istifadəçi öz söhbətini görür (həm göndərdiyi, həm aldığı mesajlar)
    public async Task<List<ChatMessageDto>> GetMyMessagesAsync(string userId)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .OrderBy(m => m.SentAt)   // köhnədən yeniyə (söhbət ardıcıllığı)
            .ToListAsync();

        return messages.Select(MapToDto).ToList();
    }

    // admin konkret istifadəçi ilə söhbəti görür
    public async Task<List<ChatMessageDto>> GetConversationAsync(string userId)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        // admin açdı → istifadəçinin mesajlarını oxunmuş işarələ
        var unread = messages.Where(m => !m.IsFromAdmin && !m.IsRead).ToList();
        if (unread.Any())
        {
            foreach (var m in unread)
                m.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return messages.Select(MapToDto).ToList();
    }

    // admin bütün söhbətlərin siyahısını görür (hər istifadəçi üzrə xülasə)
    public async Task<List<ChatConversationDto>> GetConversationsAsync()
    {
        // istifadəçilərin göndərdiyi mesajları qrupla (kim yazıb)
        var messages = await _context.ChatMessages
            .Where(m => !m.IsFromAdmin)   // istifadəçilərdən gələnlər
            .ToListAsync();

        var conversations = messages
            .GroupBy(m => new { m.SenderId, m.SenderName })
            .Select(g => new ChatConversationDto
            {
                UserId = g.Key.SenderId,
                UserName = g.Key.SenderName,
                LastMessage = g.OrderByDescending(m => m.SentAt).First().Content,
                LastMessageAt = g.Max(m => m.SentAt),
                UnreadCount = g.Count(m => !m.IsRead)
            })
            .OrderByDescending(c => c.LastMessageAt)   // ən son yazan yuxarıda
            .ToList();

        return conversations;
    }

    // köməkçi: entity → DTO
    private static ChatMessageDto MapToDto(Models.ChatMessage m) => new()
    {
        Id = m.Id,
        SenderId = m.SenderId,
        SenderName = m.SenderName,
        Content = m.Content,
        IsFromAdmin = m.IsFromAdmin,
        SentAt = m.SentAt
    };
}