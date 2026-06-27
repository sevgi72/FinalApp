using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class RegisterDto
{
    [Required(ErrorMessage = "Ad soyad mütləqdir.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email mütləqdir.")]
    [EmailAddress(ErrorMessage = "Email formatı yanlışdır.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parol mütləqdir.")]
    [MinLength(6, ErrorMessage = "Parol ən azı 6 simvol olmalıdır.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parol təkrarı mütləqdir.")]
    [Compare(nameof(Password), ErrorMessage = "Parollar uyğun gəlmir.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}