using FashionCommerce.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Settings;

public class PlatformSettingsConfiguration : IEntityTypeConfiguration<PlatformSettings>
{
    public void Configure(EntityTypeBuilder<PlatformSettings> builder)
    {
        builder.ToTable("PlatformSettings");

        builder.Property(s => s.DefaultVendorCommissionRatePercent).HasPrecision(5, 2);
        builder.Property(s => s.DefaultTaxRatePercent).HasPrecision(5, 2);
        builder.Property(s => s.DefaultShippingFee).HasPrecision(18, 2);
        builder.Property(s => s.SupportEmail).IsRequired().HasMaxLength(256);
        builder.Property(s => s.PlatformName).IsRequired().HasMaxLength(100);

        // Seeds the single settings row via migration data, so a fresh
        // database always has exactly one row at the well-known Id.
        // Uses an anonymous object (not `new PlatformSettings { ... }`)
        // because BaseEntity.Id has a protected setter — HasData works
        // against a plain property-bag here rather than needing constructor access.
        builder.HasData(new
        {
            Id = PlatformSettings.SingletonId,
            DefaultVendorCommissionRatePercent = 20.00m,
            DefaultTaxRatePercent = 0m,
            DefaultShippingFee = 0m,
            PayoutHoldDays = 7,
            SupportEmail = "support@fashioncommerce.dev",
            PlatformName = "FashionCommerce",
            MaintenanceMode = false,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsDeleted = false
        });
    }
}
