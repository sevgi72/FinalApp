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

            // Hesab nömrəsi təkrarlana bilməz
            builder.HasIndex(a => a.AccountNumber)
                .IsUnique();

            builder.Property(a => a.Balance)
                .HasColumnType("decimal(18,2)");

            builder.Property(a => a.Currency)
                .IsRequired()
                .HasMaxLength(3);

            // enum-ları bazada mətn kimi saxla (oxunaqlı olsun: "Active", "Savings")
            builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
            builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

            // İstifadəçi → Hesablar əlaqəsi (bir user, çox hesab)
            builder.HasOne(a => a.Owner)
                .WithMany(u => u.Accounts)
                .HasForeignKey(a => a.OwnerId)
                .OnDelete(DeleteBehavior.Restrict); // user silinsə hesablar avtomatik silinməsin

            // PostgreSQL concurrency — xmin sistem sütunu ilə
            //builder.UseXminAsConcurrencyToken();

            builder.Property(a => a.Version).IsRowVersion();
        }
    }
}
