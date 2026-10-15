using FashionCommerce.Domain.Entities.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Support;

public class BotConversationConfiguration : IEntityTypeConfiguration<BotConversation>
{
    public void Configure(EntityTypeBuilder<BotConversation> builder)
    {
        builder.ToTable("BotConversations");

        builder.HasIndex(b => b.UserId);
        builder.HasIndex(b => b.Channel);

        builder.HasMany(b => b.Messages)
            .WithOne(m => m.BotConversation)
            .HasForeignKey(m => m.BotConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BotMessageConfiguration : IEntityTypeConfiguration<BotMessage>
{
    public void Configure(EntityTypeBuilder<BotMessage> builder)
    {
        builder.ToTable("BotMessages");
        builder.Property(m => m.Content).IsRequired().HasMaxLength(4000);
        builder.Property(m => m.Intent).HasMaxLength(100);
        builder.HasIndex(m => m.BotConversationId);
    }
}
