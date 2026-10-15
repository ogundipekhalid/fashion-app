using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Identity;

namespace FashionCommerce.Domain.Entities.Wishlist;

public class WishlistItem : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    /// <summary>Optional: customer wishlisted a specific color/size, not just the product.</summary>
    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
