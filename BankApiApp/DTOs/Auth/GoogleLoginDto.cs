using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class GoogleLoginDto
{
    [Required(ErrorMessage = "Google token mütləqdir.")]
    public string IdToken { get; set; } = string.Empty;
}