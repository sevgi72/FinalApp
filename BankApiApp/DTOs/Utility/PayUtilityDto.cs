using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Utility;

public class PayUtilityDto
{
    [Required(ErrorMessage = "Provayder seçilməlidir.")]
    public Guid ProviderId { get; set; }

    [Required(ErrorMessage = "Abunəçi kodu mütləqdir.")]
    public string SubscriberCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Hesab seçilməlidir.")]
    public Guid AccountId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Məbləğ 0-dan böyük olmalıdır.")]
    public decimal Amount { get; set; }
}