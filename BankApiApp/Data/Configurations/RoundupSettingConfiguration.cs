using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApiApp.Data.Configurations;

public class RoundUpSettingConfiguration : IEntityTypeConfiguration<RoundUpSetting>
{
    public void Configure(EntityTypeBuilder<RoundUpSetting> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.UserId).IsRequired().HasMaxLength(450);

        // her istifadecinin bir round-up ayari olsun
        builder.HasIndex(r => r.UserId).IsUnique();
    }
}