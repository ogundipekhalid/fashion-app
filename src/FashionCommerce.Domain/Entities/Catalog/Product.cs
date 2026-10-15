using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Chat;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Domain.Events;

namespace FashionCommerce.Domain.Entities.Catalog;

public class Product : AuditableEntity
{
    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = default!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public Guid? BrandId { get; set; }
    public Brand? Brand { get; set; }

    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string Description { get; set; } = default!;

    public Gender? TargetGender { get; set; }
    public Season Season { get; set; } = Season.AllSeason;
    public string? Material { get; set; }

    public decimal BasePrice { get; set; }
    public decimal? SalePrice { get; set; }
    public DiscountType? DiscountType { get; set; }

    public string Sku { get; set; } = default!;
    public string? Barcode { get; set; }

    public decimal? WeightKg { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Draft;
    public string? RejectionReason { get; set; }

    // SEO (also AI-assistable per AI Features module)
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? SeoKeywords { get; set; }

    // Aggregate/denormalized fields, maintained by domain logic below,
    // kept in sync via application-layer handlers reacting to ProductReview changes.
    public decimal AverageRating { get; private set; }
    public int ReviewCount { get; private set; }
    public int TotalSold { get; private set; }

    /// <summary>Effective selling price, accounting for an active sale price.</summary>
    public decimal CurrentPrice => SalePrice.HasValue && SalePrice.Value < BasePrice ? SalePrice.Value : BasePrice;

    // Navigation
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductVideo> Videos { get; set; } = new List<ProductVideo>();
    public Product3DModel? Model3D { get; set; }
    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
    public ICollection<ProductTag> Tags { get; set; } = new List<ProductTag>();
    public ICollection<ProductCollection> ProductCollections { get; set; } = new List<ProductCollection>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    /// <summary>
    /// Recalculates the denormalized rating aggregate. Called by the
    /// Application layer whenever a review is added/updated/removed —
    /// kept here because "how do we average our own reviews" is a Product
    /// business rule, not an infrastructure concern.
    /// </summary>
    public void RecalculateRating(IEnumerable<ProductReview> currentReviews)
    {
        var reviews = currentReviews.Where(r => !r.IsDeleted).ToList();
        ReviewCount = reviews.Count;
        AverageRating = reviews.Count == 0 ? 0 : Math.Round((decimal)reviews.Average(r => r.Rating), 2);
    }

    public void IncrementSoldCount(int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        TotalSold += quantity;
    }

    public void Approve()
    {
        Status = ProductStatus.Active;
        RejectionReason = null;
        AddDomainEvent(new ProductApprovedEvent(Id, VendorId));
    }

    public void Reject(string reason)
    {
        Status = ProductStatus.Rejected;
        RejectionReason = reason;
        AddDomainEvent(new ProductRejectedEvent(Id, VendorId, reason));
    }
}
