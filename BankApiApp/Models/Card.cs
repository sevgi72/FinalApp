using System.ComponentModel.DataAnnotations;
using System.Security.Principal;

namespace BankApiApp.Models
{
    public class Card
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(16)]
        public string CardNumber { get; set; } = string.Empty;

        public DateTime ExpiryDate { get; set; }
        public bool IsBlocked { get; set; } = false;

        // Hansı hesaba bağlıdır
        public int AccountId { get; set; }
        public Account Account { get; set; } = null!;
    }
}
