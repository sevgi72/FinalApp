namespace BankApiApp.DTOs.Auth;

public class ProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
    public int AccountCount { get; set; }
    public decimal TotalBalance { get; set; }
}