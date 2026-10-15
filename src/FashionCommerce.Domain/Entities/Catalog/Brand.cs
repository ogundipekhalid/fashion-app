using FashionCommerce.Domain.Common;

namespace FashionCommerce.Domain.Entities.Catalog;

public class Brand : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? LogoUrl { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

/// <summary>Curated merchandising groupings, e.g. "Summer 2026", "Wedding Guest Edit".</summary>
public class Collection : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public string? BannerImageUrl { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ProductCollection> ProductCollections { get; set; } = new List<ProductCollection>();
}

/// <summary>Join entity: a Product can belong to many Collections and vice versa.</summary>
public class ProductCollection : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid CollectionId { get; set; }
    public Collection Collection { get; set; } = default!;
}
