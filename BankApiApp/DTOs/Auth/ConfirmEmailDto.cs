using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class ConfirmEmailDto
{
    [Required(ErrorMessage = "Email mutleqdir.")]
    [EmailAddress(ErrorMessage = "Email formati yanlisdir.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kod mutleqdir.")]
    public string Code { get; set; } = string.Empty;
}