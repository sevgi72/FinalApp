using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class ResendCodeDto
{
    [Required(ErrorMessage = "Email mutleqdir.")]
    [EmailAddress(ErrorMessage = "Email formati yanlisdir.")]
    public string Email { get; set; } = string.Empty;
}