using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApiApp.Data.Configurations;

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.HasKey(l => l.Id);

        // enum mətn kimi saxlansın ("Active", "Pending")
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);

        // Loan → Account (kredit bir hesaba bağlıdır)
        builder.HasOne(l => l.Account)
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Restrict);   // hesab silinsə kredit avtomatik silinməsin

        // Loan → Payments (bir kreditin çox ödəniş sətri)
        builder.HasMany(l => l.Payments)
            .WithOne(p => p.Loan)
            .HasForeignKey(p => p.LoanId)
            .OnDelete(DeleteBehavior.Cascade);    // kredit silinsə cədvəl də silinsin
    }
}