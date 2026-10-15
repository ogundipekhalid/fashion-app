using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Domain;

public class ProductVariantTests
{
    private static ProductVariant CreateVariant(int stock, int lowStockThreshold = 5) => new()
    {
        ProductId = Guid.NewGuid(),
        Color = "Blue",
        Size = "M",
        Sku = "SKU-TEST-001",
        StockQuantity = stock,
        LowStockThreshold = lowStockThreshold
    };

    [Fact]
    public void ReserveStock_decrements_quantity()
    {
        var variant = CreateVariant(stock: 10);

        variant.ReserveStock(3);

        variant.StockQuantity.Should().Be(7);
    }

    [Fact]
    public void ReserveStock_throws_when_requested_quantity_exceeds_available_stock()
    {
        var variant = CreateVariant(stock: 2);

        var act = () => variant.ReserveStock(5);

        act.Should().Throw<InsufficientStockException>()
            .Which.Available.Should().Be(2);
    }

    [Fact]
    public void ReserveStock_throws_for_zero_or_negative_quantity()
    {
        var variant = CreateVariant(stock: 10);

        var act = () => variant.ReserveStock(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReserveStock_exactly_depleting_stock_leaves_zero_not_negative()
    {
        var variant = CreateVariant(stock: 5);

        variant.ReserveStock(5);

        variant.StockQuantity.Should().Be(0);
        variant.IsInStock.Should().BeFalse();
    }

    [Fact]
    public void RestockOnCancellationOrRefund_increments_quantity()
    {
        var variant = CreateVariant(stock: 3);

        variant.RestockOnCancellationOrRefund(2);

        variant.StockQuantity.Should().Be(5);
    }

    [Theory]
    [InlineData(3, 5, true)]  // at threshold
    [InlineData(5, 5, true)]  // exactly at threshold
    [InlineData(6, 5, false)] // above threshold
    public void IsLowStock_reflects_threshold_comparison(int stock, int threshold, bool expectedLowStock)
    {
        var variant = CreateVariant(stock, threshold);

        variant.IsLowStock.Should().Be(expectedLowStock);
    }
}
