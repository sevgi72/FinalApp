using BankApiApp.Models;

namespace BankApiApp.DTOs.Account;

public class CreateAccountDto
{
    public AccountType Type { get; set; } = AccountType.Checking;
    public string Currency { get; set; } = "AZN";
}