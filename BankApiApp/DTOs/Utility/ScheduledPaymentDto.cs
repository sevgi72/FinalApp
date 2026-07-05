using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Utility;

public class CreateScheduledPaymentDto
{
    [Required(ErrorMessage = "Hesab secilmelidir.")]
    public Guid AccountId { get; set; }

    [Required(ErrorMessage = "Provayder secilmelidir.")]
    public Guid ProviderId { get; set; }

    [Required(ErrorMessage = "Abuneci kodu mutleqdir.")]
    public string SubscriberCode { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Mebleg 0-dan boyuk olmalidir.")]
    public decimal Amount { get; set; }

    [Range(1, 28, ErrorMessage = "Gun 1-28 arasi olmalidir.")]
    public int DayOfMonth { get; set; }
}

public class ScheduledPaymentDto
{
    public Guid Id { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string SubscriberCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int DayOfMonth { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastExecutedAt { get; set; }
}