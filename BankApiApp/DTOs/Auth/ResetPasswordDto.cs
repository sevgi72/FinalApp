using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class ResetPasswordDto
{
    [Required(ErrorMessage = "Email mutleqdir.")]
    [EmailAddress(ErrorMessage = "Email formati yanlisdir.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kod mutleqdir.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parol mutleqdir.")]
    [MinLength(6, ErrorMessage = "Yeni parol en azi 6 simvol olmalidir.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parol tekrari mutleqdir.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Yeni parollar uygun gelmir.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}