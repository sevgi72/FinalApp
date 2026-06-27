using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class ChangePasswordDto
{
    [Required(ErrorMessage = "Cari parol mütləqdir.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parol mütləqdir.")]
    [MinLength(6, ErrorMessage = "Yeni parol ən azı 6 simvol olmalıdır.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parol təkrarı mütləqdir.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Yeni parollar uyğun gəlmir.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}