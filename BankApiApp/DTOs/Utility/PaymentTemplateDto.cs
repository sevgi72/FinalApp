using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Utility;

public class CreatePaymentTemplateDto
{
    [Required(ErrorMessage = "Ad mutleqdir.")]
    [MaxLength(100, ErrorMessage = "Ad 100 simvoldan cox ola bilmez.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Provayder secilmelidir.")]
    public Guid ProviderId { get; set; }

    [Required(ErrorMessage = "Abuneci kodu mutleqdir.")]
    public string SubscriberCode { get; set; } = string.Empty;
}

public class PaymentTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SubscriberCode { get; set; } = string.Empty;
}