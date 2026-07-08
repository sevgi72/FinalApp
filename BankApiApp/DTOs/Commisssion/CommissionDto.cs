using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Commission;

public class CommissionRuleDto
{
    public Guid Id { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal MinAmount { get; set; }
    public decimal Percentage { get; set; }
    public decimal MaxCommission { get; set; }
    public bool IsActive { get; set; }
}

public class UpdateCommissionRuleDto
{
    [Range(0, double.MaxValue, ErrorMessage = "Minimum mebleg menfi ola bilmez.")]
    public decimal MinAmount { get; set; }

    [Range(0, 1, ErrorMessage = "Faiz 0-1 arasi olmalidir (mes 0.005 = 0.5%).")]
    public decimal Percentage { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Maksimum komissiya menfi ola bilmez.")]
    public decimal MaxCommission { get; set; }

    public bool IsActive { get; set; }
}