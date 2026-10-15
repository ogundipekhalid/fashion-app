using FashionCommerce.Domain.Entities.Cart;
using FashionCommerce.Domain.Entities.Wishlist;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Cart;

public class CartConfiguration : IEntityTypeConfiguration<Domain.Entities.Cart.Cart>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Cart.Cart> builder)
    {
        builder.ToTable("Carts");
        builder.Property(c => c.GuestSessionId).HasMaxLength(100);
        builder.Property(c => c.AppliedCouponCode).HasMaxLength(50);
        builder.Ignore(c => c.Subtotal); // computed property, not persisted

        builder.HasIndex(c => c.CustomerId);
        builder.HasIndex(c => c.GuestSessionId);

        builder.HasMany(c => c.Items)
            .WithOne(i => i.Cart)
            .HasForeignKey(i => i.CartId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems");
        builder.Property(i => i.PriceAtAdd).HasPrecision(18, 2);
        builder.Ignore(i => i.LineTotal);

        builder.HasIndex(i => new { i.CartId, i.ProductVariantId }).IsUnique();
    }
}

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("WishlistItems");
        builder.HasIndex(w => new { w.CustomerId, w.ProductId, w.ProductVariantId }).IsUnique();
    }
}
