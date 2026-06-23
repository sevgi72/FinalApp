using BankApiApp.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BankApiApp.Models
{
    public class Account : AuditableEntity   // Id, CreatedAt, UpdatedAt avtomatik gəlir
    {
        [Required]
        [MaxLength(34)]
        public string AccountNumber { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "AZN";

        public AccountType Type { get; set; } = AccountType.Checking;
        public AccountStatus Status { get; set; } = AccountStatus.Active;

        [Required]
        public string OwnerId { get; set; } = string.Empty;
        public ApplicationUser Owner { get; set; } = null!;

        //[Timestamp]
        //public uint RowVersion { get; set; }   concurrency ucn 
    }
}
