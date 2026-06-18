using BankApiApp.Models.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace BankApiApp.Models;

public class LoanPayment : BaseEntity   // yalnız Id (tarix lazım deyil)
{
    public Guid LoanId { get; set; }       // hansı kreditə aiddir
    public Loan Loan { get; set; } = null!;

    public int PaymentNumber { get; set; } // neçənci ödəniş: 1, 2, 3...

    public DateTime DueDate { get; set; }  // bu ödənişin son tarixi (məs. növbəti ayın 1-i)

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }    // bu ay ödəniləcək məbləğ (məs. 110)

    public bool IsPaid { get; set; } = false;  // ödənilibmi?
    public DateTime? PaidAt { get; set; }       // nə vaxt ödənildi (ödənilməyibsə null)
}