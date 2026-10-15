using FashionCommerce.Domain.Entities.Styling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Styling;

public class StylePostConfiguration : IEntityTypeConfiguration<StylePost>
{
    public void Configure(EntityTypeBuilder<StylePost> builder)
    {
        builder.ToTable("StylePosts");

        builder.Property(p => p.Caption).HasMaxLength(500);
        builder.Property(p => p.RemovalReason).HasMaxLength(500);

        builder.HasIndex(p => p.ProductId);
        builder.HasIndex(p => new { p.AuthorId, p.AuthorType });
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.CreatedAt);

        builder.HasOne(p => p.Product)
            .WithMany()
            .HasForeignKey(p => p.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Images)
            .WithOne(i => i.StylePost)
            .HasForeignKey(i => i.StylePostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.TaggedProducts)
            .WithOne(t => t.StylePost)
            .HasForeignKey(t => t.StylePostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Likes)
            .WithOne(l => l.StylePost)
            .HasForeignKey(l => l.StylePostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Comments)
            .WithOne(c => c.StylePost)
            .HasForeignKey(c => c.StylePostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class StylePostImageConfiguration : IEntityTypeConfiguration<StylePostImage>
{
    public void Configure(EntityTypeBuilder<StylePostImage> builder)
    {
        builder.ToTable("StylePostImages");
        builder.Property(i => i.Url).IsRequired().HasMaxLength(2048);
    }
}

public class StylePostProductTagConfiguration : IEntityTypeConfiguration<StylePostProductTag>
{
    public void Configure(EntityTypeBuilder<StylePostProductTag> builder)
    {
        builder.ToTable("StylePostProductTags");
        builder.HasIndex(t => new { t.StylePostId, t.ProductId }).IsUnique();

        builder.HasOne(t => t.Product)
            .WithMany()
            .HasForeignKey(t => t.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StylePostLikeConfiguration : IEntityTypeConfiguration<StylePostLike>
{
    public void Configure(EntityTypeBuilder<StylePostLike> builder)
    {
        builder.ToTable("StylePostLikes");

        // One like per user per post — enforced at the DB level, not just in the handler.
        builder.HasIndex(l => new { l.StylePostId, l.UserId }).IsUnique();
    }
}

public class StylePostCommentConfiguration : IEntityTypeConfiguration<StylePostComment>
{
    public void Configure(EntityTypeBuilder<StylePostComment> builder)
    {
        builder.ToTable("StylePostComments");
        builder.Property(c => c.Content).IsRequired().HasMaxLength(1000);
        builder.HasIndex(c => c.StylePostId);
    }
}
