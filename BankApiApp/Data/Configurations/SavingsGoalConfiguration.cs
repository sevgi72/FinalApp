using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApiApp.Data.Configurations;

public class SavingsGoalConfiguration : IEntityTypeConfiguration<SavingsGoal>
{
    public void Configure(EntityTypeBuilder<SavingsGoal> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.UserId).IsRequired().HasMaxLength(450);
        builder.Property(g => g.Name).IsRequired().HasMaxLength(100);
        builder.Property(g => g.TargetAmount).HasColumnType("decimal(18,2)");
        builder.Property(g => g.CurrentAmount).HasColumnType("decimal(18,2)");

        builder.HasIndex(g => g.UserId);
    }
}