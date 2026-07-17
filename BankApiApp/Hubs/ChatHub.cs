using BankApiApp.Data;
using BankApiApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BankApiApp.Hubs;

[Authorize]   // yalniz login olmus istifadeciler qosula biler
public class ChatHub : Hub
{
    private readonly AppDbContext _context;

    // ── ONLAYN VEZIYYET (Presence) YADDAŞI ──
    // userId -> acik baglanti sayi (istifadeci bir nece tab aca biler).
    // static-dir, cunki Hub her sorgu ucun yeniden yaradilir.
    private static readonly object _lock = new();
    private static readonly Dictionary<string, int> _onlineCustomers = new();
    private static readonly Dictionary<string, int> _onlineAdmins = new();

    private const string AdminsGroup = "admins";
    private const string CustomersGroup = "customers";

    public ChatHub(AppDbContext context)
    {
        _context = context;
    }

    // Istifadecinin id ve adini token-den gotur
    private string UserId => Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string UserName => Context.User!.FindFirstValue(ClaimTypes.Name) ?? "Istifadeci";
    private bool IsAdmin => Context.User!.IsInRole("Admin") || Context.User!.IsInRole("SuperAdmin");

    // Sayi artirir; TRUE = bu, istifadəçinin ilk aciq baglantisidir
    private static bool AddConnection(Dictionary<string, int> dict, string userId)
    {
        if (dict.TryGetValue(userId, out var count))
        {
            dict[userId] = count + 1;
            return false;
        }
        dict[userId] = 1;
        return true;
    }

    // Sayi azaldir; TRUE = bu, istifadəçinin sonuncu baglantisi idi (tam cixdi)
    private static bool RemoveConnection(Dictionary<string, int> dict, string userId)
    {
        if (!dict.TryGetValue(userId, out var count)) return false;
        if (count <= 1)
        {
            dict.Remove(userId);
            return true;
        }
        dict[userId] = count - 1;
        return false;
    }

    // Istifadeci qosulanda
    public override async Task OnConnectedAsync()
    {
        var userId = UserId;

        if (IsAdmin)
        {
            // Adminləri özəl qrupa yığırıq
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);

            bool supportJustCameOnline;
            string[] onlineCustomersList;

            lock (_lock)
            {
                supportJustCameOnline = _onlineAdmins.Count == 0;   // bu qoşulan ilk admindirmi?
                AddConnection(_onlineAdmins, userId);
                onlineCustomersList = _onlineCustomers.Keys.ToArray();
            }

            // Yeni qosulan admine cari onlayn olan musteri siyahisini gonder
            await Clients.Caller.SendAsync("OnlineUsers", onlineCustomersList);

            // Eger ilk admin qosuldusa — butun musterilere "destek onlayndir" siqnalı getsin
            if (supportJustCameOnline)
                await Clients.Group(CustomersGroup).SendAsync("SupportPresence", true);
        }
        else
        {
            // Müştəriləri özəl qrupa yığırıq
            await Groups.AddToGroupAsync(Context.ConnectionId, CustomersGroup);

            bool isFirst, supportOnline;
            lock (_lock)
            {
                isFirst = AddConnection(_onlineCustomers, userId);
                supportOnline = _onlineAdmins.Count > 0; // Hər hansı bir admin onlayndırmı?
            }

            // Musteriye desteyin (adminin) veziyyetini bildir (onlayn/oflayn)
            await Clients.Caller.SendAsync("SupportPresence", supportOnline);

            // Adminlere bu musterinin onlayn oldugunu bildir (siyahıda yaşıl yansın)
            if (isFirst)
                await Clients.Group(AdminsGroup).SendAsync("UserPresence", userId, true);
        }

        await base.OnConnectedAsync();
    }

    // Istifadeci ayrilanda (tabı bağlayanda və s.)
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = UserId;

        if (IsAdmin)
        {
            bool supportWentOffline;
            lock (_lock)
            {
                RemoveConnection(_onlineAdmins, userId);
                supportWentOffline = _onlineAdmins.Count == 0;   // sonuncu admin də ayrıldımı?
            }

            // Eger sistemde hec bir admin qalmadısa — musterilere bildir
            if (supportWentOffline)
                await Clients.Group(CustomersGroup).SendAsync("SupportPresence", false);
        }
        else
        {
            bool wasLast;
            lock (_lock)
            {
                wasLast = RemoveConnection(_onlineCustomers, userId);
            }

            // Eger musteri butun tabları bağlayıb tam ayrıldısa — adminlərə bildir (nöqtə bozarsın)
            if (wasLast)
                await Clients.Group(AdminsGroup).SendAsync("UserPresence", userId, false);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Admin panel siyahini el ile de yeniden sorusa biler
    public Task<string[]> GetOnlineUsers()
    {

        if (!IsAdmin) return Task.FromResult(Array.Empty<string>());
        lock (_lock)
        {
            return Task.FromResult(_onlineCustomers.Keys.ToArray());
        }
    }
    // Musteri destekin veziyyetini birbasa sorusa biler (yaris probleminden qacmaq ucun)
    public Task<bool> IsSupportOnline()
    {
        lock (_lock) { return Task.FromResult(_onlineAdmins.Count > 0); }
    }
    // ISTIFADECI → ADMIN mesaj gonderir
    public async Task SendToAdmin(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        var message = new ChatMessage
        {
            SenderId = UserId,
            SenderName = UserName,
            ReceiverId = "admin",       // adminlere umumi
            Content = content,
            IsFromAdmin = false,
            IsRead = false,
            SentAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        // butun adminlere real-time itele
        await Clients.Group(AdminsGroup).SendAsync("ReceiveMessage", new
        {
            id = message.Id,
            senderId = message.SenderId,
            senderName = message.SenderName,
            content = message.Content,
            isFromAdmin = false,
            sentAt = message.SentAt
        });

        // gonderenin ozune de tesdiq (oz mesajini gorsun)
        await Clients.Caller.SendAsync("MessageSent", new
        {
            id = message.Id,
            content = message.Content,
            sentAt = message.SentAt
        });
    }

    // ADMIN → ISTIFADECI cavab gonderir
    public async Task SendToUser(string userId, string content)
    {
        if (!IsAdmin || string.IsNullOrWhiteSpace(content))
            return;

        var message = new ChatMessage
        {
            SenderId = UserId,          // admin id
            SenderName = UserName,      // admin adi
            ReceiverId = userId,        // konkret istifadeci
            Content = content,
            IsFromAdmin = true,
            IsRead = false,
            SentAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        // konkret istifadeciye real-time itele (userId ile)
        await Clients.User(userId).SendAsync("ReceiveMessage", new
        {
            id = message.Id,
            senderId = message.SenderId,
            senderName = message.SenderName,
            content = message.Content,
            isFromAdmin = true,
            sentAt = message.SentAt
        });

        // admin ozu de gorsun
        await Clients.Caller.SendAsync("MessageSent", new
        {
            id = message.Id,
            content = message.Content,
            sentAt = message.SentAt
        });
    }
}