namespace BankApiApp.DTOs.Auth;

public class ProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }              // yeni
    public bool IsTwoFactorEnabled { get; set; }          // yeni (2FA statusu)
    public bool EmailConfirmed { get; set; }              // yeni
    public IList<string> Roles { get; set; } = new List<string>();
    public int AccountCount { get; set; }
    public decimal TotalBalance { get; set; }
    public DateTime CreatedAt { get; set; }               // yeni (qeydiyyat tarixi)
}