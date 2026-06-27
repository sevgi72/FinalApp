using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class ConfirmEmailDto
{
    [Required(ErrorMessage = "Email mütləqdir.")]
    [EmailAddress(ErrorMessage = "Email formatı yanlışdır.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kod mütləqdir.")]
    public string Code { get; set; } = string.Empty;
}