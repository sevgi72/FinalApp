using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApiApp.Data.Configurations;

public class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CardNumber).IsRequired().HasMaxLength(16);
        builder.HasIndex(c => c.CardNumber).IsUnique();   // iki eyni nomre olmasin

        builder.Property(c => c.CardHolderName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Status).HasConversion<string>();   // enum metn kimi saxlanir

        builder.HasOne(c => c.Account)
            .WithMany(a => a.Cards)
            .HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.Cascade);   // hesab silinse kartlari da getsin
    }
}