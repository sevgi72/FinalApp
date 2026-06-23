using BankApiApp.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.Security.Principal;

namespace BankApiApp.Models
{
    public class Card : AuditableEntity   // yalnız Id, tarix yoxdur
    {
        [Required]
        [MaxLength(16)]
        public string CardNumber { get; set; } = string.Empty;
        public string CardHolderName { get; set; } = string.Empty;

        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public CardStatus Status { get; set; }

        public Guid AccountId { get; set; }
        public Account Account { get; set; } = null!;
    }
}
