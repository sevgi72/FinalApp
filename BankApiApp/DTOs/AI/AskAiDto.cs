using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Ai;

public class AskAiDto
{
    [Required(ErrorMessage = "Sual bos ola bilmez.")]
    [MaxLength(1000, ErrorMessage = "Sual cox uzundur.")]
    public string Question { get; set; } = string.Empty;
}