namespace BankApiApp.DTOs.Transactions;

public class StatementDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal OpeningBalance { get; set; }   // baslangic balans
    public decimal ClosingBalance { get; set; }   // son balans
    public decimal TotalIn { get; set; }          // umumi giris
    public decimal TotalOut { get; set; }         // umumi cixis
    public int Count { get; set; }
    public List<StatementItemDto> Items { get; set; } = new();
}

public class StatementItemDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public bool IsIncoming { get; set; }          // bu hesaba giris?
    public decimal? BalanceAfter { get; set; }    // emeliyyatdan sonra balans
}