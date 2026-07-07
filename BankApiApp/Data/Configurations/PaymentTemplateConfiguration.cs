using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApiApp.Data.Configurations;

public class PaymentTemplateConfiguration : IEntityTypeConfiguration<PaymentTemplate>
{
    public void Configure(EntityTypeBuilder<PaymentTemplate> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.UserId).IsRequired().HasMaxLength(450);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.SubscriberCode).IsRequired().HasMaxLength(50);

        builder.HasOne(t => t.Provider)
            .WithMany()
            .HasForeignKey(t => t.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.UserId);
    }
}