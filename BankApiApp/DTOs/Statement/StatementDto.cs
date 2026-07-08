namespace BankApiApp.DTOs.Statement;

public class StatementRequestDto
{
    public Guid AccountId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

public class StatementDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    public decimal OpeningBalance { get; set; }    // dovrun evvelinde balans
    public decimal ClosingBalance { get; set; }    // dovrun sonunda balans

    public decimal TotalIn { get; set; }           // umumi daxil olan
    public decimal TotalOut { get; set; }          // umumi cixan
    public int TransactionCount { get; set; }

    public List<StatementLineDto> Lines { get; set; } = new();
}

public class StatementLineDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string Direction { get; set; } = string.Empty;   // "In" ve ya "Out"
    public decimal? BalanceAfter { get; set; }              // snapshot (F4-den)
}