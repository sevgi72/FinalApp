using BankApiApp.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.Security.Principal;

namespace BankApiApp.Models
{
    public class Card : AuditableEntity   // yalniz Id, tarix yoxdur
    {
        [Required]
        [MaxLength(16)]
        public string CardNumber { get; set; } = string.Empty;
        public string CardHolderName { get; set; } = string.Empty;

        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public CardStatus Status { get; set; }

        // ── Yeni: kart parametrleri ──
        [MaxLength(40)]
        public string? CardName { get; set; }                    // istifadeci verdiyi ad (Esas kartim)
        public decimal MonthlyLimit { get; set; } = 0;           // 0 = limitsiz
        public bool OnlinePaymentsEnabled { get; set; } = true;  // onlayn odenisler acikdir?

        public Guid AccountId { get; set; }
        public Account Account { get; set; } = null!;
    }
}