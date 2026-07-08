using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApiApp.Data.Configurations;

public class CommissionRuleConfiguration : IEntityTypeConfiguration<CommissionRule>
{
    public void Configure(EntityTypeBuilder<CommissionRule> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.TransactionType).IsRequired().HasMaxLength(30);
        builder.Property(c => c.MinAmount).HasColumnType("decimal(18,2)");
        builder.Property(c => c.Percentage).HasColumnType("decimal(5,4)");
        builder.Property(c => c.MaxCommission).HasColumnType("decimal(18,2)");
    }
}