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
        builder.HasIndex(c => c.CardNumber).IsUnique();   // iki eyni nömrə olmasın

        builder.Property(c => c.CardHolderName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Status).HasConversion<string>();   // enum mətn kimi saxlanır

        builder.HasOne(c => c.Account)
            .WithMany(a => a.Cards)
            .HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.Cascade);   // hesab silinsə kartları da getsin
    }
}