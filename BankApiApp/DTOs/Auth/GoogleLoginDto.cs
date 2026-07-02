using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class GoogleLoginDto
{
    [Required(ErrorMessage = "Google token mutleqdir.")]
    public string IdToken { get; set; } = string.Empty;
}