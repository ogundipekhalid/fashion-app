using FashionCommerce.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Orders;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(30);
        builder.Property(o => o.AppliedCouponCode).HasMaxLength(50);
        builder.Property(o => o.CancellationReason).HasMaxLength(500);

        builder.Property(o => o.Subtotal).HasPrecision(18, 2);
        builder.Property(o => o.DiscountAmount).HasPrecision(18, 2);
        builder.Property(o => o.TaxAmount).HasPrecision(18, 2);
        builder.Property(o => o.ShippingFee).HasPrecision(18, 2);
        builder.Property(o => o.Total).HasPrecision(18, 2);

        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.PlacedAt);

        builder.Property(o => o.RowVersion).IsRowVersion();

        // Status is set via TransitionTo(); expose as a normal column but
        // prevent EF from generating a public setter requirement by backing field.
        builder.Property(o => o.Status).HasConversion<int>();
        builder.Property(o => o.PaymentStatus).HasConversion<int>();

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.StatusHistory)
            .WithOne(h => h.Order)
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Payments)
            .WithOne(p => p.Order)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.Property(i => i.ProductNameSnapshot).IsRequired().HasMaxLength(250);
        builder.Property(i => i.ColorSnapshot).IsRequired().HasMaxLength(50);
        builder.Property(i => i.SizeSnapshot).IsRequired().HasMaxLength(20);
        builder.Property(i => i.ImageUrlSnapshot).HasMaxLength(2048);

        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
        builder.Property(i => i.CommissionRatePercent).HasPrecision(5, 2);
        builder.Property(i => i.CommissionAmount).HasPrecision(18, 2);
        builder.Property(i => i.VendorEarning).HasPrecision(18, 2);
        builder.Ignore(i => i.LineSubtotal);

        builder.HasIndex(i => i.OrderId);
        builder.HasIndex(i => i.VendorId);
        builder.HasIndex(i => i.Status);

        builder.HasOne(i => i.Vendor)
            .WithMany()
            .HasForeignKey(i => i.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.ProductVariant)
            .WithMany()
            .HasForeignKey(i => i.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistories");
        builder.Property(h => h.Note).HasMaxLength(500);
        builder.HasIndex(h => h.OrderId);
    }
}
