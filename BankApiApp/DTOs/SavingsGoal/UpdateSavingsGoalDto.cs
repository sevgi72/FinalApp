using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.SavingsGoal;

public class UpdateSavingsGoalDto
{
    [Required(ErrorMessage = "Ad mutleqdir.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(1, double.MaxValue, ErrorMessage = "Hedef mebleg musbet olmalidir.")]
    public decimal TargetAmount { get; set; }

    public DateTime? Deadline { get; set; }
}