using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class ForgotPasswordDto
{
    [Required(ErrorMessage = "Email mütləqdir.")]
    [EmailAddress(ErrorMessage = "Email formatı yanlışdır.")]
    public string Email { get; set; } = string.Empty;
}