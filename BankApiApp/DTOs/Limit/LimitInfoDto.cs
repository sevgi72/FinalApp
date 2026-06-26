namespace BankApiApp.DTOs.Limits;

public class LimitInfoDto
{
    public decimal DailyLimit { get; set; }
    public decimal DailyUsed { get; set; }
    public decimal DailyRemaining { get; set; }

    public decimal MonthlyLimit { get; set; }
    public decimal MonthlyUsed { get; set; }
    public decimal MonthlyRemaining { get; set; }

    public decimal YearlyLimit { get; set; }
    public decimal YearlyUsed { get; set; }
    public decimal YearlyRemaining { get; set; }

    public bool IsCustom { get; set; }   // hesaba özəl limit qoyulub, yoxsa standart?
}