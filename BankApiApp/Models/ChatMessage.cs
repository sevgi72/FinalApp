using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class ChatMessage : BaseEntity   // Id buradan
{
    public string SenderId { get; set; } = string.Empty;      // kim gonderdi (userId)
    public string SenderName { get; set; } = string.Empty;    // gonderenin adi (gostermek ucun)
    public string ReceiverId { get; set; } = string.Empty;    // kime (userId) — admin cavabinda istifadeci, istifadeci mesajinda admin/bos
    public string Content { get; set; } = string.Empty;       // mesaj metni
    public bool IsFromAdmin { get; set; }                     // admin yazdi, yoxsa istifadeci?
    public bool IsRead { get; set; }                          // oxunub?
    public DateTime SentAt { get; set; }                      // ne vaxt
}