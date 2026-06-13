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

        // IAuditableEntity-dən gələn sahələr
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Account> Accounts { get; set; } = new List<Account>();
    }

}
