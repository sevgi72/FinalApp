using BankApiApp.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Principal;
using System.Transactions;

namespace BankApiApp.Models
{
    public class Transaction : AuditableEntity   // Id + CreatedAt buradan gelir
    {
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public TransactionType Type { get; set; }
        public TransactionStatus Status { get; set; } = TransactionStatus.Completed;

        [MaxLength(250)]
        public string? Description { get; set; }

        public Guid? FromAccountId { get; set; }
        public Account? FromAccount { get; set; }

        public Guid? ToAccountId { get; set; }
        public Account? ToAccount { get; set; }
    }
}
