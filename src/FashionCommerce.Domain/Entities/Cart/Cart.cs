using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Identity;

namespace FashionCommerce.Domain.Entities.Cart;

/// <summary>
/// Supports both guest (SessionId-based) and authenticated (CustomerId-based)
/// carts. When a guest logs in, the Application layer merges the guest cart
/// into the customer's cart (see MergeGuestCartCommand, built in a later module).
/// </summary>
public class Cart : AuditableEntity
{
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Anonymous/guest identifier, stored client-side (cookie/localStorage) until login.</summary>
    public string? GuestSessionId { get; set; }

    public string? AppliedCouponCode { get; set; }

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();

    public decimal Subtotal => Items.Sum(i => i.LineTotal);
}

public class CartItem : AuditableEntity
{
    public Guid CartId { get; set; }
    public Cart Cart { get; set; } = default!;

    public Guid ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = default!;

    public int Quantity { get; set; }

    /// <summary>Price snapshot at the moment the item was added, so display stays stable even if the vendor changes the price later (recalculated again at checkout).</summary>
    public decimal PriceAtAdd { get; set; }

    public decimal LineTotal => PriceAtAdd * Quantity;
}
