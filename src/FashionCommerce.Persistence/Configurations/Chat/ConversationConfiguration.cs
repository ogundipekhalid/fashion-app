using FashionCommerce.Domain.Entities.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Chat;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");

        builder.Property(c => c.Subject).HasMaxLength(300);
        builder.Property(c => c.ReportReason).HasMaxLength(1000);
        builder.Property(c => c.AutoFlagReason).HasMaxLength(500);

        builder.HasIndex(c => c.CustomerId);
        builder.HasIndex(c => c.VendorId);
        builder.HasIndex(c => c.ProductId); // fast "show me all conversations about this product" for vendors
        builder.HasIndex(c => c.OrderId);
        builder.HasIndex(c => c.RefundRequestId);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.IsAutoFlagged);

        // ProductId is required — every conversation is anchored to a product,
        // per the platform's "chat must stay on-topic" requirement. Restrict
        // (not Cascade/SetNull) since a product being removed shouldn't
        // silently delete or orphan its conversation history.
        builder.HasOne(c => c.Product)
            .WithMany(p => p.Conversations)
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");

        builder.Property(m => m.Content).HasMaxLength(4000);
        builder.Property(m => m.AttachmentUrl).HasMaxLength(2048);
        builder.Property(m => m.BlockReason).HasMaxLength(500);

        builder.HasIndex(m => m.ConversationId);
        builder.HasIndex(m => m.SentAt);
        builder.HasIndex(m => m.IsBlocked);
    }
}
