using BankApiApp.DTOs.Auth;

namespace BankApiApp.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto dto);
    Task ChangePasswordAsync(string userId, ChangePasswordDto dto);
    Task LogoutAsync(string userId, RefreshTokenRequestDto dto);
    Task<ProfileDto> GetProfileAsync(string userId);
}