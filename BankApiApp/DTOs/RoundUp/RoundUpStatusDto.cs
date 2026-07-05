namespace BankApiApp.DTOs.RoundUp;

public class RoundUpStatusDto
{
    public bool IsEnabled { get; set; }
    public Guid? SourceAccountId { get; set; }
    public Guid? TargetAccountId { get; set; }
    public int RoundTo { get; set; }
    public decimal TotalSavedThisMonth { get; set; }   // bu ay yigilan
    public decimal TotalSavedAllTime { get; set; }     // umumi yigilan
}