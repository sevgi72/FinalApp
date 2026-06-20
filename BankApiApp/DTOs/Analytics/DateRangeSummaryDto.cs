namespace BankApiApp.DTOs.Analytics;

public class DateRangeSummaryDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetFlow { get; set; }
    public List<DailySummaryDto> DailyBreakdown { get; set; } = new();
}

public class DailySummaryDto
{
    public DateTime Date { get; set; }
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
}