namespace BankApiApp.Models
{
    public enum AccountType
    {
        Checking = 1,   // cari hesab
        Savings = 2     // emanet hesabi
    }

    public enum AccountStatus
    {
        Active = 1,
        Blocked = 2,
        Closed = 3
    }

    public enum TransactionType
    {
        Deposit = 1,    // pul yatirma
        Withdraw = 2,   // pul cixarma
        Transfer = 3    // kocurme
    }

    public enum TransactionStatus
    {
        Pending = 1,
        Completed = 2,
        Failed = 3
    }
    public enum LoanStatus
    {
        Pending = 1,    // muraciet edilib, admin tesdiqini gozleyir
        Active = 2,     // tesdiqlenib, odenilir
        Rejected = 3,   // redd edilib
        Completed = 4   // tam odenilib, bitdi
    }
    public enum CardStatus
    {
        Active = 1,
        Blocked = 2
    }

    public enum PaymentStatus
    {
        Pending = 1,
        Completed = 2,
        Failed = 3
    }

    public enum UtilityCategory
    {
        Electricity = 1,   // Elektrik
        Water = 2,         // Su
        Gas = 3,           // Qaz
        Mobile = 4,        // Mobil
        Internet = 5,      // Internet
        TV = 6             // Televiziya
    }
}
