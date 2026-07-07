using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApiApp.Data.Configurations;

public class DepositConfiguration : IEntityTypeConfiguration<Deposit>
{
    public void Configure(EntityTypeBuilder<Deposit> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.UserId).IsRequired().HasMaxLength(450);
        builder.Property(d => d.Principal).HasColumnType("decimal(18,2)");
        builder.Property(d => d.EarnedInterest).HasColumnType("decimal(18,2)");
        builder.Property(d => d.AnnualRate).HasColumnType("decimal(5,4)");

        builder.HasIndex(d => d.UserId);
    }
}