using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class ForgotPasswordDto
{
    [Required(ErrorMessage = "Email mutleqdir.")]
    [EmailAddress(ErrorMessage = "Email formati yanlisdir.")]
    public string Email { get; set; } = string.Empty;
}