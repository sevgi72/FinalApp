using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApiApp.Data.Configurations;

public class UtilityPaymentConfiguration : IEntityTypeConfiguration<UtilityPayment>
{
    public void Configure(EntityTypeBuilder<UtilityPayment> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.SubscriberCode).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Status).HasMaxLength(20);

        // provayder əlaqəsi
        builder.HasOne(p => p.Provider)
            .WithMany()
            .HasForeignKey(p => p.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.AccountId);
    }
}