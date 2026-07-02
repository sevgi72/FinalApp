using BankApiApp.Models;
using BankApiApp.Models.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using System.Security.Principal;

namespace BankApiApp.Models
{
    public class ApplicationUser : IdentityUser, IAuditableEntity   // BaseEntity yox, interfeys!
    {
        public string FullName { get; set; } = string.Empty;

        // IAuditableEntity-den gelen saheler
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Account> Accounts { get; set; } = new List<Account>();
        // Email tesdiqi ucun
        public string? EmailConfirmationCode { get; set; }
        public DateTime? EmailConfirmationCodeExpiry { get; set; }

        // Parol sifirlama ucun
        public string? PasswordResetCode { get; set; }
        public DateTime? PasswordResetCodeExpiry { get; set; }

        // 2FA / OTP ucun
        public bool IsTwoFactorEnabled { get; set; } = false;   // istifadeci secimi
        public string? TwoFactorCode { get; set; }
        public DateTime? TwoFactorCodeExpiry { get; set; }

        // Google login ucun
        public string? GoogleId { get; set; }   // Google istifadecisinin unikal ID-si (null = adi qeydiyyat)
    }

}
