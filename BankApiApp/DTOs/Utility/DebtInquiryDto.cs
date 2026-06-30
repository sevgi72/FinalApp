using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Utility;

public class DebtInquiryDto
{
    [Required(ErrorMessage = "Provayder seçilməlidir.")]
    public Guid ProviderId { get; set; }

    [Required(ErrorMessage = "Abunəçi kodu mütləqdir.")]
    public string SubscriberCode { get; set; } = string.Empty;
}