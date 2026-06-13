namespace BankApiApp.Models
{
    public enum AccountType
    {
        Checking = 1,   // cari hesab
        Savings = 2     // əmanət hesabı
    }

    public enum AccountStatus
    {
        Active = 1,
        Blocked = 2,
        Closed = 3
    }

    public enum TransactionType
    {
        Deposit = 1,    // pul yatırma
        Withdraw = 2,   // pul çıxarma
        Transfer = 3    // köçürmə
    }

    public enum TransactionStatus
    {
        Pending = 1,
        Completed = 2,
        Failed = 3
    }
}
