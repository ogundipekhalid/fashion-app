using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Exceptions;

namespace FashionCommerce.Domain.Entities.Catalog;

/// <summary>
/// A purchasable SKU-level variant of a Product (specific color + size
/// combination). Stock and price live here, not on Product, since a Product
/// can have many variants each with independent inventory.
/// </summary>
public class ProductVariant : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public string Color { get; set; } = default!;
    public string? ColorHexCode { get; set; }
    public string Size { get; set; } = default!;

    public string Sku { get; set; } = default!;
    public string? Barcode { get; set; }

    /// <summary>Overrides Product.BasePrice when set; null means "use parent product price".</summary>
    public decimal? PriceOverride { get; set; }

    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public bool IsActive { get; set; } = true;

    public bool IsLowStock => StockQuantity <= LowStockThreshold;
    public bool IsInStock => StockQuantity > 0;

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

    /// <summary>
    /// Decrements stock, enforcing the invariant that stock can never go
    /// negative. Called from the Order placement flow inside a DB
    /// transaction with row locking to avoid overselling.
    /// </summary>
    public void ReserveStock(int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (StockQuantity < quantity)
            throw new InsufficientStockException(ProductId, Id, quantity, StockQuantity);

        StockQuantity -= quantity;
    }

    public void RestockOnCancellationOrRefund(int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        StockQuantity += quantity;
    }
}
