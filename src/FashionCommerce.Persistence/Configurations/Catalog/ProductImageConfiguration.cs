using FashionCommerce.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Catalog;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.Property(i => i.Url).IsRequired().HasMaxLength(2048);
        builder.Property(i => i.AltText).HasMaxLength(300);
        builder.HasIndex(i => i.ProductId);
    }
}

public class ProductVideoConfiguration : IEntityTypeConfiguration<ProductVideo>
{
    public void Configure(EntityTypeBuilder<ProductVideo> builder)
    {
        builder.ToTable("ProductVideos");
        builder.Property(v => v.Url).IsRequired().HasMaxLength(2048);
        builder.Property(v => v.ThumbnailUrl).HasMaxLength(2048);
    }
}

public class Product3DModelConfiguration : IEntityTypeConfiguration<Product3DModel>
{
    public void Configure(EntityTypeBuilder<Product3DModel> builder)
    {
        builder.ToTable("Product3DModels");
        builder.Property(m => m.GlbUrl).HasMaxLength(2048);
        builder.Property(m => m.UsdzUrl).HasMaxLength(2048);
        builder.HasIndex(m => m.ProductId).IsUnique();
    }
}
