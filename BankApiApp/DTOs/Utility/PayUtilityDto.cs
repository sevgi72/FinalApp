using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Utility;

public class PayUtilityDto
{
    [Required(ErrorMessage = "Provayder secilmelidir.")]
    public Guid ProviderId { get; set; }

    [Required(ErrorMessage = "Abuneci kodu mutleqdir.")]
    public string SubscriberCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Hesab secilmelidir.")]
    public Guid AccountId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Mebleg 0-dan boyuk olmalidir.")]
    public decimal Amount { get; set; }
}