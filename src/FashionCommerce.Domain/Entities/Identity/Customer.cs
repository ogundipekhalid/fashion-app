using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Cart;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Orders;
using FashionCommerce.Domain.Entities.Wishlist;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Domain.Entities.Identity;

/// <summary>
/// The Customer-facing profile. Deliberately decoupled from ASP.NET Core
/// Identity: <see cref="IdentityUserId"/> is a foreign key to the
/// AspNetUsers table owned by the Persistence/Infrastructure layer, but the
/// Domain project itself never references Microsoft.AspNetCore.Identity.
/// This keeps authentication a pluggable concern.
/// </summary>
public class Customer : AuditableEntity
{
    public Guid IdentityUserId { get; set; }

    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? ProfileImageUrl { get; set; }

    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }

    /// <summary>Loyalty/reward points balance, spendable at checkout (future coupon integration).</summary>
    public int LoyaltyPoints { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    // Navigation
    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Cart.Cart> Carts { get; set; } = new List<Cart.Cart>();
    public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
    public ICollection<FavoriteVendor> FavoriteVendors { get; set; } = new List<FavoriteVendor>();
}
