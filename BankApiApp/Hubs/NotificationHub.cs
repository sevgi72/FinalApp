using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BankApiApp.Hubs;

// bildirisler ucun canli baglanti (yalniz qebul - tek terefli)
[Authorize]
public class NotificationHub : Hub
{
    // istifadeci qosulanda avtomatik oz "user" qrupuna dusur
    // (SignalR Clients.User(userId) ile hedeflemek ucun elave is lazim deyil)
}