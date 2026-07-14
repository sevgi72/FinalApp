using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Auth;

public class UpdateProfileDto
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Phone]
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }
}