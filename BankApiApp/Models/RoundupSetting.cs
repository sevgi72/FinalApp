using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class RoundUpSetting : AuditableEntity   // Id, CreatedAt, UpdatedAt
{
    public string UserId { get; set; } = string.Empty;      // kimin ayari
    public Guid SourceAccountId { get; set; }               // izlenen hesab (xercler burdan)
    public Guid TargetAccountId { get; set; }               // yigim hesabi (ferqler bura)
    public int RoundTo { get; set; } = 1;                   // yuvarlaqlasdirma: 1 ve ya 5 AZN
    public bool IsEnabled { get; set; } = true;             // aktiv/deaktiv
}