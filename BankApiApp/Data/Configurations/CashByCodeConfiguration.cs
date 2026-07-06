using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApiApp.Data.Configurations;

public class CashByCodeConfiguration : IEntityTypeConfiguration<CashByCode>
{
    public void Configure(EntityTypeBuilder<CashByCode> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(8);
        builder.Property(c => c.Amount).HasColumnType("decimal(18,2)");
        builder.Property(c => c.Status).HasMaxLength(20);
        builder.Property(c => c.SenderUserId).IsRequired().HasMaxLength(450);

        builder.HasIndex(c => c.Code).IsUnique();   // kod unikaldir
        builder.HasIndex(c => c.SenderUserId);
    }
}