using BankApiApp.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.Security.Principal;

namespace BankApiApp.Models
{
    public class Card : BaseEntity   // yalnız Id, tarix yoxdur
    {
        [Required]
        [MaxLength(16)]
        public string CardNumber { get; set; } = string.Empty;

        public DateTime ExpiryDate { get; set; }
        public bool IsBlocked { get; set; } = false;

        public Guid AccountId { get; set; }
        public Account Account { get; set; } = null!;
    }
}
