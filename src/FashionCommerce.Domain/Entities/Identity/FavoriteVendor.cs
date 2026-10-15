using FashionCommerce.Domain.Common;

namespace FashionCommerce.Domain.Entities.Identity;

/// <summary>Explicit join entity (rather than an implicit EF many-to-many) so we can store when the favorite was added.</summary>
public class FavoriteVendor : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = default!;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
