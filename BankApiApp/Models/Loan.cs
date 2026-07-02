using BankApiApp.Models.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace BankApiApp.Models;

public class Loan : AuditableEntity   // Id + CreatedAt + UpdatedAt buradan gelir
{
    // Krediti alan hesab: pul bura gelir, ayliq odenisler buradan tutulur
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Principal { get; set; }          // goturulen esas mebleg (mes. 1200)

    [Column(TypeName = "decimal(5,2)")]
    public decimal InterestRate { get; set; }       // illik faiz % (mes. 10)

    public int TermMonths { get; set; }             // nece ay (mes. 12)

    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyPayment { get; set; }     // ayliq odenis — sistem hesablayir

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPayable { get; set; }       // umumi qaytarilacaq — sistem hesablayir

    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingBalance { get; set; }   // qaliq borc — her odenisde azalir

    public LoanStatus Status { get; set; } = LoanStatus.Pending;

    public DateTime? StartDate { get; set; }        // tesdiqlenme tarixi (tesdiqlenene qeder null)

    // Bu kreditin odenis cedveli (12 ay → 12 setir)
    public ICollection<LoanPayment> Payments { get; set; } = new List<LoanPayment>();
}