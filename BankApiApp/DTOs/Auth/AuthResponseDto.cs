namespace BankApiApp.DTOs.Auth;

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;   
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    public bool RequiresTwoFactor { get; set; } = false;
    public string? Message { get; set; }
}