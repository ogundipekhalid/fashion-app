using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Domain.Entities.Catalog;

public class ProductImage : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    /// <summary>Optional: image belongs to a specific color variant rather than the whole product.</summary>
    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public string Url { get; set; } = default!;
    public string? AltText { get; set; }
    public ProductImageAngle Angle { get; set; } = ProductImageAngle.Front;
    public ProductMediaGenerationSource Source { get; set; } = ProductMediaGenerationSource.VendorUploaded;

    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
}

public class ProductVideo : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public string Url { get; set; } = default!;
    public string? ThumbnailUrl { get; set; }
    public int? DurationSeconds { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// 3D/AR asset for the interactive product viewer (360-degree rotation,
/// zoom). GLB serves web/Android via &lt;model-viewer&gt;, USDZ serves iOS AR
/// Quick Look.
/// </summary>
public class Product3DModel : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public string? GlbUrl { get; set; }
    public string? UsdzUrl { get; set; }
    public bool SupportsArQuickLook { get; set; }
    public bool Is360Enabled { get; set; }
}
