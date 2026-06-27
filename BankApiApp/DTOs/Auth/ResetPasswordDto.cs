using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class ResetPasswordDto
{
    [Required(ErrorMessage = "Email mütləqdir.")]
    [EmailAddress(ErrorMessage = "Email formatı yanlışdır.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kod mütləqdir.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parol mütləqdir.")]
    [MinLength(6, ErrorMessage = "Yeni parol ən azı 6 simvol olmalıdır.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parol təkrarı mütləqdir.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Yeni parollar uyğun gəlmir.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}