using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class RegisterDto
{
    [Required(ErrorMessage = "Ad soyad mutleqdir.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email mutleqdir.")]
    [EmailAddress(ErrorMessage = "Email formati yanlisdir.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parol mutleqdir.")]
    [MinLength(6, ErrorMessage = "Parol en azi 6 simvol olmalidir.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parol tekrari mutleqdir.")]
    [Compare(nameof(Password), ErrorMessage = "Parollar uygun gelmir.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}