using FashionCommerce.Domain.Entities.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Payments;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.Property(p => p.ProviderReference).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.FailureReason).HasMaxLength(500);
        builder.Property(p => p.RawResponseJson).HasColumnType("nvarchar(max)");
        builder.Property(p => p.Amount).HasPrecision(18, 2);

        builder.HasIndex(p => p.ProviderReference).IsUnique();
        builder.HasIndex(p => p.OrderId);

        builder.HasMany(p => p.WebhookLogs)
            .WithOne(w => w.Payment)
            .HasForeignKey(w => w.PaymentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class PaymentWebhookLogConfiguration : IEntityTypeConfiguration<PaymentWebhookLog>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookLog> builder)
    {
        builder.ToTable("PaymentWebhookLogs");

        builder.Property(w => w.ProviderEventId).IsRequired().HasMaxLength(200);
        builder.Property(w => w.EventType).IsRequired().HasMaxLength(100);
        builder.Property(w => w.PayloadJson).IsRequired().HasColumnType("nvarchar(max)");

        // Idempotency: never process the same provider event twice.
        builder.HasIndex(w => new { w.Provider, w.ProviderEventId }).IsUnique();
    }
}
