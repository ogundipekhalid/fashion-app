using FashionCommerce.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Catalog;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.Property(p => p.Name).IsRequired().HasMaxLength(250);
        builder.Property(p => p.Slug).IsRequired().HasMaxLength(280);
        builder.Property(p => p.Description).IsRequired();
        builder.Property(p => p.Material).HasMaxLength(150);
        builder.Property(p => p.Sku).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Barcode).HasMaxLength(100);
        builder.Property(p => p.MetaTitle).HasMaxLength(200);
        builder.Property(p => p.MetaDescription).HasMaxLength(500);
        builder.Property(p => p.SeoKeywords).HasMaxLength(500);
        builder.Property(p => p.RejectionReason).HasMaxLength(1000);

        builder.Property(p => p.BasePrice).HasPrecision(18, 2);
        builder.Property(p => p.SalePrice).HasPrecision(18, 2);
        builder.Property(p => p.WeightKg).HasPrecision(10, 3);
        builder.Property(p => p.LengthCm).HasPrecision(10, 2);
        builder.Property(p => p.WidthCm).HasPrecision(10, 2);
        builder.Property(p => p.HeightCm).HasPrecision(10, 2);
        builder.Property(p => p.AverageRating).HasPrecision(3, 2);

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.Sku).IsUnique();
        builder.HasIndex(p => p.VendorId);
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.Status);

        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Brand)
            .WithMany(b => b.Products)
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(p => p.Variants)
            .WithOne(v => v.Product)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Images)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Videos)
            .WithOne(v => v.Product)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Model3D)
            .WithOne(m => m.Product)
            .HasForeignKey<Product3DModel>(m => m.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Reviews)
            .WithOne(r => r.Product)
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Tags)
            .WithOne(t => t.Product)
            .HasForeignKey(t => t.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants");

        builder.Property(v => v.Color).IsRequired().HasMaxLength(50);
        builder.Property(v => v.ColorHexCode).HasMaxLength(9);
        builder.Property(v => v.Size).IsRequired().HasMaxLength(20);
        builder.Property(v => v.Sku).IsRequired().HasMaxLength(100);
        builder.Property(v => v.Barcode).HasMaxLength(100);
        builder.Property(v => v.PriceOverride).HasPrecision(18, 2);

        builder.HasIndex(v => v.Sku).IsUnique();
        builder.HasIndex(v => new { v.ProductId, v.Color, v.Size }).IsUnique();

        builder.Property(v => v.RowVersion).IsRowVersion(); // critical: stock decrements need optimistic concurrency to prevent overselling

        builder.HasMany(v => v.Images)
            .WithOne(i => i.ProductVariant)
            .HasForeignKey(i => i.ProductVariantId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
