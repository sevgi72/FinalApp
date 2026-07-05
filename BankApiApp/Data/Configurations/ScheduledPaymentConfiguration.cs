using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApiApp.Data.Configurations;

public class ScheduledPaymentConfiguration : IEntityTypeConfiguration<ScheduledPayment>
{
    public void Configure(EntityTypeBuilder<ScheduledPayment> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.UserId).IsRequired().HasMaxLength(450);
        builder.Property(s => s.SubscriberCode).IsRequired().HasMaxLength(50);
        builder.Property(s => s.Amount).HasColumnType("decimal(18,2)");

        builder.HasOne(s => s.Provider)
            .WithMany()
            .HasForeignKey(s => s.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.UserId);
    }
}