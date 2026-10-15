using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Domain.Exceptions;

namespace FashionCommerce.Domain.Entities.Marketing;

public class Coupon : AuditableEntity
{
    public string Code { get; set; } = default!;
    public CouponType Type { get; set; }
    public decimal Value { get; set; } // percentage or fixed amount, per Type

    public decimal? MinOrderAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; } // cap for percentage coupons

    /// <summary>Null = platform-wide coupon; set = vendor-specific coupon (applies only to that vendor's items).</summary>
    public Guid? VendorId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public int? UsageLimit { get; set; }
    public int? UsageLimitPerCustomer { get; set; }
    public int TimesUsed { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<CouponUsage> Usages { get; set; } = new List<CouponUsage>();

    public bool IsCurrentlyValid(DateTime asOf) =>
        IsActive && asOf >= StartDate && asOf <= EndDate && (UsageLimit is null || TimesUsed < UsageLimit);

    public decimal CalculateDiscount(decimal orderSubtotal)
    {
        if (MinOrderAmount.HasValue && orderSubtotal < MinOrderAmount.Value)
            throw new InvalidCouponException($"Order subtotal must be at least {MinOrderAmount.Value} to use this coupon.");

        var discount = Type switch
        {
            CouponType.Percentage => orderSubtotal * (Value / 100m),
            CouponType.FixedAmount => Value,
            CouponType.FreeShipping => 0, // shipping fee waived separately
            _ => 0
        };

        if (MaxDiscountAmount.HasValue) discount = Math.Min(discount, MaxDiscountAmount.Value);
        return Math.Round(Math.Min(discount, orderSubtotal), 2);
    }
}

public class CouponUsage : BaseEntity
{
    public Guid CouponId { get; set; }
    public Coupon Coupon { get; set; } = default!;

    public Guid CustomerId { get; set; }
    public Guid OrderId { get; set; }
    public decimal DiscountApplied { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
}
