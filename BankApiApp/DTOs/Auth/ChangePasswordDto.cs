using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class ChangePasswordDto
{
    [Required(ErrorMessage = "Cari parol mutleqdir.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parol mutleqdir.")]
    [MinLength(6, ErrorMessage = "Yeni parol en azi 6 simvol olmalidir.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parol tekrari mutleqdir.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Yeni parollar uygun gelmir.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}