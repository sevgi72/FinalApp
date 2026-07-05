namespace BankApiApp.DTOs.Admin;

public class AdminStatsDto
{
    // bu gun
    public int TodayTransactionCount { get; set; }
    public decimal TodayTransactionVolume { get; set; }

    // bu hefte / bu ay
    public int WeekTransactionCount { get; set; }
    public decimal WeekTransactionVolume { get; set; }
    public int MonthTransactionCount { get; set; }
    public decimal MonthTransactionVolume { get; set; }

    // umumi
    public int TotalUsers { get; set; }
    public int BlockedUsers { get; set; }
    public int TotalAccounts { get; set; }
    public decimal TotalBalance { get; set; }

    // top-lar
    public List<TopUserDto> TopActiveUsers { get; set; } = new();
    public List<ProviderVolumeDto> UtilityByProvider { get; set; } = new();
}

public class TopUserDto
{
    public string Email { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalVolume { get; set; }
}

public class ProviderVolumeDto
{
    public string ProviderName { get; set; } = string.Empty;
    public int PaymentCount { get; set; }
    public decimal TotalAmount { get; set; }
}