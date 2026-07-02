using BankApiApp.DTOs.Auth;
using BankApiApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankApiApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
        => Ok(new { message = await _authService.RegisterAsync(dto) });

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailDto dto)
        => Ok(new { message = await _authService.ConfirmEmailAsync(dto) });

    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendCode(ResendCodeDto dto)
        => Ok(new { message = await _authService.ResendConfirmationCodeAsync(dto) });

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
        => Ok(await _authService.LoginAsync(dto));

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(VerifyTwoFactorDto dto)
        => Ok(await _authService.VerifyTwoFactorAsync(dto));

    [HttpPost("toggle-2fa")]
    [Authorize]
    public async Task<IActionResult> ToggleTwoFactor(ToggleTwoFactorDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(new { message = await _authService.ToggleTwoFactorAsync(userId, dto) });
    }

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin(GoogleLoginDto dto)
        => Ok(await _authService.GoogleLoginAsync(dto));

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(RefreshTokenRequestDto dto)
        => Ok(await _authService.RefreshTokenAsync(dto));

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        => Ok(new { message = await _authService.ForgotPasswordAsync(dto) });

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        => Ok(new { message = await _authService.ResetPasswordAsync(dto) });

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _authService.ChangePasswordAsync(userId, dto);
        return Ok(new { message = "Parol ugurla deyisdirildi." });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(RefreshTokenRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _authService.LogoutAsync(userId, dto);
        return Ok(new { message = "Cixis edildi." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _authService.GetProfileAsync(userId));
    }
}