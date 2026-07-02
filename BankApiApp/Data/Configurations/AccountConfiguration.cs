using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApiApp.Data.Configurations
{
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            builder.HasKey(a => a.Id);

            builder.Property(a => a.AccountNumber)
                .IsRequired()
                .HasMaxLength(34);

            // Hesab nomresi tekrarlana bilmez
            builder.HasIndex(a => a.AccountNumber)
                .IsUnique();

            builder.Property(a => a.Balance)
                .HasColumnType("decimal(18,2)");

            builder.Property(a => a.Currency)
                .IsRequired()
                .HasMaxLength(3);

            // enum-lari bazada metn kimi saxla (oxunaqli olsun: "Active", "Savings")
            builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
            builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

            // Istifadeci → Hesablar elaqesi (bir user, cox hesab)
            builder.HasOne(a => a.Owner)
                .WithMany(u => u.Accounts)
                .HasForeignKey(a => a.OwnerId)
                .OnDelete(DeleteBehavior.Restrict); // user silinse hesablar avtomatik silinmesin

            // PostgreSQL concurrency — xmin sistem sutunu ile
            //builder.UseXminAsConcurrencyToken();

            builder.Property(a => a.Version).IsRowVersion();
        }
    }
}
