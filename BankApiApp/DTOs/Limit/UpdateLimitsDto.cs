namespace BankApiApp.DTOs.Limits;

public class UpdateLimitsDto
{
    public decimal? DailyLimit { get; set; }    // null → standart limitə qayıt
    public decimal? MonthlyLimit { get; set; }
    public decimal? YearlyLimit { get; set; }
}