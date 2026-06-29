using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApiApp.Data.Configurations;

public class UtilityProviderConfiguration : IEntityTypeConfiguration<UtilityProvider>
{
    public void Configure(EntityTypeBuilder<UtilityProvider> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Category).HasConversion<string>().HasMaxLength(20);
    }
}