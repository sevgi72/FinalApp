using BankApiApp.Models.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace BankApiApp.Models;

public class LoanPayment : BaseEntity   // yalniz Id (tarix lazim deyil)
{
    public Guid LoanId { get; set; }       // hansi kredite aiddir
    public Loan Loan { get; set; } = null!;

    public int PaymentNumber { get; set; } // necenci odenis: 1, 2, 3...

    public DateTime DueDate { get; set; }  // bu odenisin son tarixi (mes. novbeti ayin 1-i)

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }    // bu ay odenilecek mebleg (mes. 110)

    public bool IsPaid { get; set; } = false;  // odenilibmi?
    public DateTime? PaidAt { get; set; }       // ne vaxt odenildi (odenilmeyibse null)
}