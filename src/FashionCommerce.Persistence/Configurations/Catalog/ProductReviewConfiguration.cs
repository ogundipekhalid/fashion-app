using FashionCommerce.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Catalog;

public class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> builder)
    {
        builder.ToTable("ProductReviews");

        builder.Property(r => r.Title).HasMaxLength(200);
        builder.Property(r => r.Comment).HasMaxLength(2000);
        builder.Property(r => r.AiSummary).HasMaxLength(500);

        // One review per customer per product.
        builder.HasIndex(r => new { r.ProductId, r.CustomerId }).IsUnique();

        builder.HasMany(r => r.Images)
            .WithOne(i => i.ProductReview)
            .HasForeignKey(i => i.ProductReviewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductReviewImageConfiguration : IEntityTypeConfiguration<ProductReviewImage>
{
    public void Configure(EntityTypeBuilder<ProductReviewImage> builder)
    {
        builder.ToTable("ProductReviewImages");
        builder.Property(i => i.Url).IsRequired().HasMaxLength(2048);
    }
}

public class ProductTagConfiguration : IEntityTypeConfiguration<ProductTag>
{
    public void Configure(EntityTypeBuilder<ProductTag> builder)
    {
        builder.ToTable("ProductTags");
        builder.Property(t => t.Tag).IsRequired().HasMaxLength(100);
        builder.HasIndex(t => new { t.ProductId, t.Tag }).IsUnique();
    }
}
