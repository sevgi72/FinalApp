using BankApiApp.Models.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace BankApiApp.Models;

public class Loan : AuditableEntity   // Id + CreatedAt + UpdatedAt buradan gəlir
{
    // Krediti alan hesab: pul bura gəlir, aylıq ödənişlər buradan tutulur
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Principal { get; set; }          // götürülən əsas məbləğ (məs. 1200)

    [Column(TypeName = "decimal(5,2)")]
    public decimal InterestRate { get; set; }       // illik faiz % (məs. 10)

    public int TermMonths { get; set; }             // neçə ay (məs. 12)

    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyPayment { get; set; }     // aylıq ödəniş — sistem hesablayır

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPayable { get; set; }       // ümumi qaytarılacaq — sistem hesablayır

    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingBalance { get; set; }   // qalıq borc — hər ödənişdə azalır

    public LoanStatus Status { get; set; } = LoanStatus.Pending;

    public DateTime? StartDate { get; set; }        // təsdiqlənmə tarixi (təsdiqlənənə qədər null)

    // Bu kreditin ödəniş cədvəli (12 ay → 12 sətir)
    public ICollection<LoanPayment> Payments { get; set; } = new List<LoanPayment>();
}