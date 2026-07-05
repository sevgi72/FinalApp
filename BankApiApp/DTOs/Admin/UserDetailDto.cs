namespace BankApiApp.DTOs.Admin;

public class UserDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public bool IsBlocked { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public DateTime CreatedAt { get; set; }

    // maliyye xulasesi
    public int AccountCount { get; set; }
    public decimal TotalBalance { get; set; }
    public List<UserAccountSummaryDto> Accounts { get; set; } = new();
    public List<UserTransactionSummaryDto> RecentTransactions { get; set; } = new();
}

public class UserAccountSummaryDto
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class UserTransactionSummaryDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}