using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.SavingsGoal;

public class CreateSavingsGoalDto
{
    [Required(ErrorMessage = "Ad mutleqdir.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(1, double.MaxValue, ErrorMessage = "Hedef mebleg 0-dan boyuk olmalidir.")]
    public decimal TargetAmount { get; set; }

    [Required(ErrorMessage = "Hesab secilmelidir.")]
    public Guid SourceAccountId { get; set; }

    public DateTime? Deadline { get; set; }
}

public class ContributeGoalDto
{
    [Range(0.01, double.MaxValue, ErrorMessage = "Mebleg 0-dan boyuk olmalidir.")]
    public decimal Amount { get; set; }
}

public class SavingsGoalDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public decimal Percentage { get; set; }        // proqres %
    public bool IsCompleted { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime CreatedAt { get; set; }
}