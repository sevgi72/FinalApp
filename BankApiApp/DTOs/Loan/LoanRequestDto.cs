namespace BankApiApp.DTOs.Loan;

public class LoanRequestDto
{
    public Guid AccountId { get; set; }   // hansi hesaba
    public decimal Principal { get; set; } // ne qeder kredit
    public int TermMonths { get; set; }    // nece aya
}