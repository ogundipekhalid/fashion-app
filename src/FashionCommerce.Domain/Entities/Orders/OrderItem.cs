using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Domain.Entities.Orders;

/// <summary>
/// A single line item, always scoped to exactly one Vendor. This is where
/// the marketplace commission split is computed and frozen at the moment
/// of purchase (so later commission-rate changes on Vendor don't retroactively
/// alter historical orders).
/// </summary>
public class OrderItem : AuditableEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = default!;

    public Guid ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = default!;

    // Snapshots at time of purchase — never re-read from Product later.
    public string ProductNameSnapshot { get; set; } = default!;
    public string ColorSnapshot { get; set; } = default!;
    public string SizeSnapshot { get; set; } = default!;
    public string? ImageUrlSnapshot { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineSubtotal => UnitPrice * Quantity;

    /// <summary>Commission rate frozen at purchase time (copied from Vendor.CommissionRatePercent).</summary>
    public decimal CommissionRatePercent { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal VendorEarning { get; set; }

    public OrderItemStatus Status { get; set; } = OrderItemStatus.Pending;

    public void ComputeCommissionSplit()
    {
        CommissionAmount = Math.Round(LineSubtotal * (CommissionRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
        VendorEarning = LineSubtotal - CommissionAmount;
    }
}
