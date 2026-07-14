using BankApiApp.DTOs.Auth;

namespace BankApiApp.Services;

public interface IAuthService
{
    Task<string> RegisterAsync(RegisterDto dto);
    Task<string> ConfirmEmailAsync(ConfirmEmailDto dto);
    Task<string> ResendConfirmationCodeAsync(ResendCodeDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto dto);
    Task ChangePasswordAsync(string userId, ChangePasswordDto dto);
    Task LogoutAsync(string userId, RefreshTokenRequestDto dto);
    Task<ProfileDto> GetProfileAsync(string userId);
    Task<string> ForgotPasswordAsync(ForgotPasswordDto dto);
    Task<string> ResetPasswordAsync(ResetPasswordDto dto);

    // 2FA 
    Task<AuthResponseDto> VerifyTwoFactorAsync(VerifyTwoFactorDto dto);
    Task<string> ToggleTwoFactorAsync(string userId, ToggleTwoFactorDto dto);

    // Google login 
    Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginDto dto);
    Task<ProfileDto> UpdateProfileAsync(string userId, UpdateProfileDto dto);

}