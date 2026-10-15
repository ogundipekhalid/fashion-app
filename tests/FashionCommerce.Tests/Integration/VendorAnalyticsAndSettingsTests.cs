using FashionCommerce.Application.Features.Admin.Commands.UpdatePlatformSettings;
using FashionCommerce.Application.Features.Admin.Queries.GetPlatformSettings;
using FashionCommerce.Application.Features.Admin.Queries.GetRevenueTrend;
using FashionCommerce.Application.Features.Vendors.Queries.GetVendorSalesTrend;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Entities.Orders;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class VendorAnalyticsAndSettingsTests
{
    [Fact]
    public async Task Vendor_sales_trend_reports_vendor_earnings_not_gross_order_totals()
    {
        await using var context = TestDbContextFactory.Create();
        var (vendorId, variantId) = await SeedVendorProductAsync(context);
        var day = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

        // 1000 gross at 20% commission => vendor earns 800. The trend should show 800, not 1000.
        var item = BuildOrderItem(vendorId, variantId, unitPrice: 1000m, commissionRate: 20m, createdAt: day);
        context.OrderItems.Add(item);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetVendorSalesTrendQueryHandler(context);
        var result = await handler.Handle(new GetVendorSalesTrendQuery
        {
            VendorId = vendorId,
            FromDate = day.AddDays(-1),
            ToDate = day.AddDays(1),
            Granularity = TrendGranularity.Daily
        }, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Revenue.Should().Be(800m); // post-commission, the vendor's actual take
    }

    [Fact]
    public async Task Vendor_sales_trend_excludes_cancelled_items()
    {
        await using var context = TestDbContextFactory.Create();
        var (vendorId, variantId) = await SeedVendorProductAsync(context);
        var day = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

        var good = BuildOrderItem(vendorId, variantId, 500m, 20m, day);
        var cancelled = BuildOrderItem(vendorId, variantId, 900m, 20m, day);
        cancelled.Status = OrderItemStatus.Cancelled;

        context.OrderItems.AddRange(good, cancelled);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetVendorSalesTrendQueryHandler(context);
        var result = await handler.Handle(new GetVendorSalesTrendQuery
        {
            VendorId = vendorId,
            FromDate = day.AddDays(-1),
            ToDate = day.AddDays(1)
        }, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Revenue.Should().Be(400m); // only the non-cancelled item's 500 * 0.8
    }

    [Fact]
    public async Task Vendor_sales_trend_does_not_leak_another_vendors_sales()
    {
        await using var context = TestDbContextFactory.Create();
        var (vendorA, variantA) = await SeedVendorProductAsync(context);
        var (vendorB, variantB) = await SeedVendorProductAsync(context);
        var day = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

        context.OrderItems.AddRange(
            BuildOrderItem(vendorA, variantA, 100m, 20m, day),
            BuildOrderItem(vendorB, variantB, 900m, 20m, day));
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetVendorSalesTrendQueryHandler(context);
        var result = await handler.Handle(new GetVendorSalesTrendQuery
        {
            VendorId = vendorA,
            FromDate = day.AddDays(-1),
            ToDate = day.AddDays(1)
        }, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Revenue.Should().Be(80m); // vendor A only — vendor B's 900 is invisible
    }

    [Fact]
    public async Task Platform_settings_are_created_on_first_read_with_sensible_defaults()
    {
        await using var context = TestDbContextFactory.Create();
        var handler = new GetPlatformSettingsQueryHandler(context);

        var result = await handler.Handle(new GetPlatformSettingsQuery(), CancellationToken.None);

        result.DefaultVendorCommissionRatePercent.Should().Be(20.00m);
        result.PayoutHoldDays.Should().Be(7);
        result.MaintenanceMode.Should().BeFalse();
        context.Settings.Local.Should().ContainSingle(); // the get-or-create fallback wrote exactly one row
    }

    [Fact]
    public async Task Updating_platform_settings_persists_and_reads_back_without_creating_a_second_row()
    {
        await using var context = TestDbContextFactory.Create();
        var updateHandler = new UpdatePlatformSettingsCommandHandler(context);

        var updateResult = await updateHandler.Handle(new UpdatePlatformSettingsCommand
        {
            DefaultVendorCommissionRatePercent = 15m,
            DefaultTaxRatePercent = 7.5m,
            DefaultShippingFee = 1500m,
            PayoutHoldDays = 14,
            SupportEmail = "help@fashioncommerce.dev",
            PlatformName = "FashionCommerce NG",
            MaintenanceMode = true
        }, CancellationToken.None);

        updateResult.Succeeded.Should().BeTrue();

        var readHandler = new GetPlatformSettingsQueryHandler(context);
        var settings = await readHandler.Handle(new GetPlatformSettingsQuery(), CancellationToken.None);

        settings.DefaultVendorCommissionRatePercent.Should().Be(15m);
        settings.DefaultTaxRatePercent.Should().Be(7.5m);
        settings.PayoutHoldDays.Should().Be(14);
        settings.PlatformName.Should().Be("FashionCommerce NG");
        settings.MaintenanceMode.Should().BeTrue();

        context.Settings.Local.Should().ContainSingle(); // still exactly one settings row — singleton invariant holds
    }

    private static async Task<(Guid VendorId, Guid VariantId)> SeedVendorProductAsync(TestApplicationDbContext context)
    {
        var vendor = new Vendor { IdentityUserId = Guid.NewGuid(), BusinessName = $"V-{Guid.NewGuid():N}", ContactEmail = $"{Guid.NewGuid():N}@test.dev" };
        var category = new Category { Name = "Shirts", Slug = $"shirts-{Guid.NewGuid():N}" };
        var product = new Product
        {
            VendorId = vendor.Id, Vendor = vendor, CategoryId = category.Id, Category = category,
            Name = "Test Shirt", Slug = $"shirt-{Guid.NewGuid():N}", Description = "desc",
            Sku = $"SKU-{Guid.NewGuid():N}", BasePrice = 100m
        };
        var variant = new ProductVariant
        {
            ProductId = product.Id, Product = product, Color = "Blue", Size = "L",
            Sku = $"SKU-VAR-{Guid.NewGuid():N}", StockQuantity = 50
        };

        context.Vendors.Add(vendor);
        context.Categories.Add(category);
        context.Products.Add(product);
        context.ProductVariants.Add(variant);
        await context.SaveChangesAsync(CancellationToken.None);

        return (vendor.Id, variant.Id);
    }

    private static OrderItem BuildOrderItem(Guid vendorId, Guid variantId, decimal unitPrice, decimal commissionRate, DateTime createdAt)
    {
        var item = new OrderItem
        {
            OrderId = Guid.NewGuid(),
            VendorId = vendorId,
            ProductVariantId = variantId,
            ProductNameSnapshot = "Test Shirt",
            ColorSnapshot = "Blue",
            SizeSnapshot = "L",
            Quantity = 1,
            UnitPrice = unitPrice,
            CommissionRatePercent = commissionRate,
            Status = OrderItemStatus.Delivered,
            CreatedAt = createdAt
        };
        item.ComputeCommissionSplit();
        return item;
    }
}
