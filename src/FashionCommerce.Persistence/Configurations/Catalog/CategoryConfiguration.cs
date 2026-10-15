using FashionCommerce.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Catalog;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(180);
        builder.Property(c => c.MetaTitle).HasMaxLength(200);
        builder.Property(c => c.MetaDescription).HasMaxLength(500);

        builder.HasIndex(c => c.Slug).IsUnique();

        builder.HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        builder.Property(b => b.Name).IsRequired().HasMaxLength(150);
        builder.Property(b => b.Slug).IsRequired().HasMaxLength(180);
        builder.HasIndex(b => b.Slug).IsUnique();
    }
}

public class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.ToTable("Collections");
        builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(180);
        builder.HasIndex(c => c.Slug).IsUnique();
    }
}

public class ProductCollectionConfiguration : IEntityTypeConfiguration<ProductCollection>
{
    public void Configure(EntityTypeBuilder<ProductCollection> builder)
    {
        builder.ToTable("ProductCollections");
        builder.HasIndex(pc => new { pc.ProductId, pc.CollectionId }).IsUnique();

        builder.HasOne(pc => pc.Product).WithMany(p => p.ProductCollections)
            .HasForeignKey(pc => pc.ProductId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pc => pc.Collection).WithMany(c => c.ProductCollections)
            .HasForeignKey(pc => pc.CollectionId).OnDelete(DeleteBehavior.Cascade);
    }
}
