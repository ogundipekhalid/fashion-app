using FashionCommerce.Domain.Entities.Marketing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Marketing;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("Coupons");

        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Value).HasPrecision(18, 2);
        builder.Property(c => c.MinOrderAmount).HasPrecision(18, 2);
        builder.Property(c => c.MaxDiscountAmount).HasPrecision(18, 2);

        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasIndex(c => c.VendorId);

        builder.HasMany(c => c.Usages)
            .WithOne(u => u.Coupon)
            .HasForeignKey(u => u.CouponId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CouponUsageConfiguration : IEntityTypeConfiguration<CouponUsage>
{
    public void Configure(EntityTypeBuilder<CouponUsage> builder)
    {
        builder.ToTable("CouponUsages");
        builder.Property(u => u.DiscountApplied).HasPrecision(18, 2);
        builder.HasIndex(u => new { u.CouponId, u.CustomerId });
    }
}
