using FashionCommerce.Domain.Entities.Orders;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Domain;

public class OrderItemTests
{
    [Fact]
    public void ComputeCommissionSplit_calculates_commission_and_vendor_earning_correctly()
    {
        var item = new OrderItem
        {
            UnitPrice = 1000m,
            Quantity = 2, // LineSubtotal = 2000
            CommissionRatePercent = 20m
        };

        item.ComputeCommissionSplit();

        item.CommissionAmount.Should().Be(400m); // 20% of 2000
        item.VendorEarning.Should().Be(1600m);   // 2000 - 400
    }

    [Fact]
    public void ComputeCommissionSplit_rounds_to_two_decimal_places()
    {
        var item = new OrderItem
        {
            UnitPrice = 33.33m,
            Quantity = 1,
            CommissionRatePercent = 15m
        };

        item.ComputeCommissionSplit();

        // 33.33 * 0.15 = 4.9995 -> rounds to 5.00
        item.CommissionAmount.Should().Be(5.00m);
        item.VendorEarning.Should().Be(28.33m);
    }

    [Fact]
    public void ComputeCommissionSplit_with_zero_commission_gives_vendor_the_full_amount()
    {
        var item = new OrderItem
        {
            UnitPrice = 500m,
            Quantity = 1,
            CommissionRatePercent = 0m
        };

        item.ComputeCommissionSplit();

        item.CommissionAmount.Should().Be(0m);
        item.VendorEarning.Should().Be(500m);
    }
}
