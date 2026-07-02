using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankApiApp.Data.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.SenderId).IsRequired().HasMaxLength(450);
        builder.Property(m => m.SenderName).HasMaxLength(256);
        builder.Property(m => m.ReceiverId).HasMaxLength(450);
        builder.Property(m => m.Content).IsRequired().HasMaxLength(2000);

        builder.HasIndex(m => m.SenderId);
        builder.HasIndex(m => m.ReceiverId);
        builder.HasIndex(m => m.SentAt);
    }
}