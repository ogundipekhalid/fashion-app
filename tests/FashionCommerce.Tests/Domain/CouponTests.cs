using FashionCommerce.Domain.Entities.Marketing;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Domain;

public class CouponTests
{
    [Fact]
    public void Percentage_coupon_calculates_correct_discount()
    {
        var coupon = new Coupon { Type = CouponType.Percentage, Value = 10m };

        var discount = coupon.CalculateDiscount(orderSubtotal: 200m);

        discount.Should().Be(20m);
    }

    [Fact]
    public void FixedAmount_coupon_returns_the_fixed_value_when_within_subtotal()
    {
        var coupon = new Coupon { Type = CouponType.FixedAmount, Value = 15m };

        var discount = coupon.CalculateDiscount(orderSubtotal: 100m);

        discount.Should().Be(15m);
    }

    [Fact]
    public void FixedAmount_coupon_never_discounts_more_than_the_subtotal()
    {
        var coupon = new Coupon { Type = CouponType.FixedAmount, Value = 500m };

        var discount = coupon.CalculateDiscount(orderSubtotal: 50m);

        discount.Should().Be(50m);
    }

    [Fact]
    public void Percentage_coupon_respects_MaxDiscountAmount_cap()
    {
        var coupon = new Coupon { Type = CouponType.Percentage, Value = 50m, MaxDiscountAmount = 30m };

        var discount = coupon.CalculateDiscount(orderSubtotal: 200m); // 50% would be 100, capped to 30

        discount.Should().Be(30m);
    }

    [Fact]
    public void MinOrderAmount_not_met_throws_InvalidCouponException()
    {
        var coupon = new Coupon { Type = CouponType.FixedAmount, Value = 10m, MinOrderAmount = 100m };

        var act = () => coupon.CalculateDiscount(orderSubtotal: 50m);

        act.Should().Throw<InvalidCouponException>();
    }

    [Fact]
    public void FreeShipping_coupon_type_returns_zero_line_discount()
    {
        var coupon = new Coupon { Type = CouponType.FreeShipping, Value = 0m };

        var discount = coupon.CalculateDiscount(orderSubtotal: 100m);

        discount.Should().Be(0m); // shipping fee waiver is applied separately, not as a subtotal discount
    }

    [Fact]
    public void IsCurrentlyValid_is_false_before_start_date()
    {
        var coupon = new Coupon
        {
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(10)
        };

        coupon.IsCurrentlyValid(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsCurrentlyValid_is_false_after_end_date()
    {
        var coupon = new Coupon
        {
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-10),
            EndDate = DateTime.UtcNow.AddDays(-1)
        };

        coupon.IsCurrentlyValid(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsCurrentlyValid_is_false_when_usage_limit_reached()
    {
        var coupon = new Coupon
        {
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            UsageLimit = 5,
            TimesUsed = 5
        };

        coupon.IsCurrentlyValid(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsCurrentlyValid_is_true_within_window_and_under_usage_limit()
    {
        var coupon = new Coupon
        {
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            UsageLimit = 5,
            TimesUsed = 2
        };

        coupon.IsCurrentlyValid(DateTime.UtcNow).Should().BeTrue();
    }
}
