namespace BankApiApp.DTOs.Loan;

public class LoanRequestDto
{
    public Guid AccountId { get; set; }   // hansı hesaba
    public decimal Principal { get; set; } // nə qədər kredit
    public int TermMonths { get; set; }    // neçə aya
}