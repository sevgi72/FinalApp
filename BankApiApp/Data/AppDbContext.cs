using BankApiApp.Models;
using BankApiApp.Models.Common;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Reflection.Emit;

namespace BankApiApp.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Cədvəllərimiz
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<Card> Cards => Set<Card>();
        public DbSet<Loan> Loans => Set<Loan>();
        public DbSet<LoanPayment> LoanPayments => Set<LoanPayment>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<UtilityProvider> UtilityProviders => Set<UtilityProvider>();
        public DbSet<UtilityPayment> UtilityPayments => Set<UtilityPayment>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // VACİB: Identity cədvəllərini (Users, Roles və s.) qurur. Mütləq əvvəl çağırılır.
            base.OnModelCreating(modelBuilder);

            // Bütün IEntityTypeConfiguration siniflərini avtomatik tapıb tətbiq edir
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }

        // Tarixləri avtomatik dolduran override
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
            {
                if (entry.State == EntityState.Added)
                    entry.Entity.CreatedAt = DateTime.UtcNow;

                if (entry.State == EntityState.Modified)
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
