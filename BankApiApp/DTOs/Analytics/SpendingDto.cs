namespace BankApiApp.DTOs.Analytics;

public class SpendingReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalSpent { get; set; }
    public List<CategorySpendingDto> Categories { get; set; } = new();
}

public class CategorySpendingDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }   // umumi xercden payi
}

public class MonthlySpendingDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalSpent { get; set; }
}