using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.CashByCode;

public class CreateCashByCodeDto
{
    [Required(ErrorMessage = "Hesab secilmelidir.")]
    public Guid AccountId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Mebleg 0-dan boyuk olmalidir.")]
    public decimal Amount { get; set; }
}

public class ClaimCashByCodeDto
{
    [Required(ErrorMessage = "Kod mutleqdir.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Hesab secilmelidir.")]
    public Guid AccountId { get; set; }   // pul hansi hesaba dussun
}

public class CashByCodeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}