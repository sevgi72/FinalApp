namespace BankApiApp.DTOs.Cashback;

public class CashbackStatusDto
{
    public decimal CashbackBalance { get; set; }
    public decimal TotalEarnedThisMonth { get; set; }
}

public class RedeemCashbackDto
{
    public Guid TargetAccountId { get; set; }
}