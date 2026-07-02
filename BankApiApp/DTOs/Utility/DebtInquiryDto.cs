using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Utility;

public class DebtInquiryDto
{
    [Required(ErrorMessage = "Provayder secilmelidir.")]
    public Guid ProviderId { get; set; }

    [Required(ErrorMessage = "Abuneci kodu mutleqdir.")]
    public string SubscriberCode { get; set; } = string.Empty;
}