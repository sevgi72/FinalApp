namespace BankApiApp.DTOs.Analytics;

public class DashboardDto
{
    public decimal TotalBalance { get; set; }      // bütün hesabların cəmi
    public int AccountCount { get; set; }
    public decimal TotalIncome { get; set; }        // ümumi gəlir
    public decimal TotalExpense { get; set; }       // ümumi xərc
    public decimal NetFlow { get; set; }            // gəlir - xərc
    public int TransactionCount { get; set; }
    public List<MonthlySummaryDto> MonthlyBreakdown { get; set; } = new();
    public List<TypeSummaryDto> ByType { get; set; } = new();
}

public class MonthlySummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;   // "2026-06"
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
}

public class TypeSummaryDto
{
    public string Type { get; set; } = string.Empty;    // Deposit / Withdraw / Transfer
    public decimal Total { get; set; }
    public int Count { get; set; }
}