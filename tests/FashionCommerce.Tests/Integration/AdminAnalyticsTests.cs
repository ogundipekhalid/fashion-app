using FashionCommerce.Application.Features.Admin.Queries.GetDashboardSummary;
using FashionCommerce.Application.Features.Admin.Queries.GetRevenueTrend;
using FashionCommerce.Application.Features.Admin.Queries.GetTaxReport;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Entities.Orders;
using FashionCommerce.Domain.Entities.Payouts;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class AdminAnalyticsTests
{
    [Fact]
    public async Task Revenue_trend_only_counts_orders_that_were_actually_paid()
    {
        await using var context = TestDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var day = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

        // Two paid orders and one that never completed payment — the unpaid one must not inflate revenue.
        context.Orders.AddRange(
            BuildOrder(customerId, day, total: 1000m, PaymentStatus.Successful),
            BuildOrder(customerId, day, total: 500m, PaymentStatus.Successful),
            BuildOrder(customerId, day, total: 9999m, PaymentStatus.Pending));
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetRevenueTrendQueryHandler(context);
        var result = await handler.Handle(new GetRevenueTrendQuery
        {
            FromDate = day.AddDays(-1),
            ToDate = day.AddDays(1),
            Granularity = TrendGranularity.Daily
        }, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Revenue.Should().Be(1500m); // the 9999 pending order is excluded
        result[0].OrderCount.Should().Be(2);
    }

    [Fact]
    public async Task Revenue_trend_groups_orders_into_separate_daily_buckets()
    {
        await using var context = TestDbContextFactory.Create();
        var customerId = Guid.NewGuid();

        context.Orders.AddRange(
            BuildOrder(customerId, new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc), 100m, PaymentStatus.Successful),
            BuildOrder(customerId, new DateTime(2026, 3, 10, 21, 0, 0, DateTimeKind.Utc), 200m, PaymentStatus.Successful), // same day, later hour
            BuildOrder(customerId, new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc), 400m, PaymentStatus.Successful));
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetRevenueTrendQueryHandler(context);
        var result = await handler.Handle(new GetRevenueTrendQuery
        {
            FromDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ToDate = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc),
            Granularity = TrendGranularity.Daily
        }, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Revenue.Should().Be(300m); // both March 10 orders collapse into one bucket
        result[1].Revenue.Should().Be(400m);
        result.Should().BeInAscendingOrder(p => p.PeriodStart);
    }

    [Fact]
    public async Task Monthly_granularity_collapses_a_whole_month_into_one_bucket()
    {
        await using var context = TestDbContextFactory.Create();
        var customerId = Guid.NewGuid();

        context.Orders.AddRange(
            BuildOrder(customerId, new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc), 100m, PaymentStatus.Successful),
            BuildOrder(customerId, new DateTime(2026, 3, 28, 9, 0, 0, DateTimeKind.Utc), 250m, PaymentStatus.Successful),
            BuildOrder(customerId, new DateTime(2026, 4, 2, 9, 0, 0, DateTimeKind.Utc), 75m, PaymentStatus.Successful));
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetRevenueTrendQueryHandler(context);
        var result = await handler.Handle(new GetRevenueTrendQuery
        {
            FromDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Granularity = TrendGranularity.Monthly
        }, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].PeriodLabel.Should().Be("2026-03");
        result[0].Revenue.Should().Be(350m);
        result[1].PeriodLabel.Should().Be("2026-04");
        result[1].Revenue.Should().Be(75m);
    }

    [Fact]
    public async Task Revenue_trend_returns_an_empty_list_rather_than_failing_when_there_are_no_orders()
    {
        await using var context = TestDbContextFactory.Create();
        var handler = new GetRevenueTrendQueryHandler(context);

        var result = await handler.Handle(new GetRevenueTrendQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Tax_report_sums_tax_collected_and_computes_the_effective_rate()
    {
        await using var context = TestDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var day = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

        var orderA = BuildOrder(customerId, day, total: 1075m, PaymentStatus.Successful);
        orderA.Subtotal = 1000m;
        orderA.TaxAmount = 75m;

        var orderB = BuildOrder(customerId, day, total: 2150m, PaymentStatus.Successful);
        orderB.Subtotal = 2000m;
        orderB.TaxAmount = 150m;

        context.Orders.AddRange(orderA, orderB);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTaxReportQueryHandler(context);
        var result = await handler.Handle(new GetTaxReportQuery
        {
            FromDate = day.AddDays(-1),
            ToDate = day.AddDays(1)
        }, CancellationToken.None);

        result.OrderCount.Should().Be(2);
        result.TotalSubtotal.Should().Be(3000m);
        result.TotalTaxCollected.Should().Be(225m);
        result.EffectiveTaxRatePercent.Should().Be(7.50m);
    }
    [Fact]
    public async Task Tax_report_reports_a_zero_rate_instead_of_dividing_by_zero_when_there_is_no_revenue()
    {
        await using var context = TestDbContextFactory.Create();
        var handler = new GetTaxReportQueryHandler(context);

        var result = await handler.Handle(new GetTaxReportQuery(), CancellationToken.None);

        result.OrderCount.Should().Be(0);
        result.EffectiveTaxRatePercent.Should().Be(0m); // no divide-by-zero
    }

    [Fact]
    public async Task Dashboard_summary_counts_pending_work_across_every_module()
    {
        await using var context = TestDbContextFactory.Create();

        var vendor = new Vendor
        {
            IdentityUserId = Guid.NewGuid(), BusinessName = "Pending Vendor", ContactEmail = "pending@test.dev",
            VerificationStatus = VendorVerificationStatus.Pending
        };
        var category = new Category { Name = "Shirts", Slug = $"shirts-{Guid.NewGuid():N}" };
        var pendingProduct = new Product
        {
            VendorId = vendor.Id, Vendor = vendor, CategoryId = category.Id, Category = category,
            Name = "Awaiting approval", Slug = $"p-{Guid.NewGuid():N}", Description = "d",
            Sku = $"SKU-{Guid.NewGuid():N}", BasePrice = 10m, Status = ProductStatus.PendingApproval
        };
        var customer = new Customer { IdentityUserId = Guid.NewGuid(), FirstName = "A", LastName = "B", Email = "c@test.dev" };

        var paidOrder = BuildOrder(customer.Id, DateTime.UtcNow, total: 500m, PaymentStatus.Successful);

        var settledCommission = new CommissionTransaction
        {
            OrderItemId = Guid.NewGuid(), VendorId = vendor.Id, Vendor = vendor,
            GrossAmount = 500m, CommissionRatePercent = 20m, CommissionAmount = 100m, VendorEarning = 400m,
            Status = CommissionTransactionStatus.Settled
        };

        context.Vendors.Add(vendor);
        context.Categories.Add(category);
        context.Products.Add(pendingProduct);
        context.Customers.Add(customer);
        context.Orders.Add(paidOrder);
        context.CommissionTransactions.Add(settledCommission);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetDashboardSummaryQueryHandler(context);
        var result = await handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        result.TotalRevenue.Should().Be(500m);
        result.TotalOrders.Should().Be(1);
        result.TotalCustomers.Should().Be(1);
        result.TotalVendors.Should().Be(1);
        result.PendingVendorVerifications.Should().Be(1);
        result.PendingProductApprovals.Should().Be(1);
        result.TotalPlatformCommissionEarned.Should().Be(100m); // only settled commissions count
    }

    private static Order BuildOrder(Guid customerId, DateTime placedAt, decimal total, PaymentStatus paymentStatus)
    {
        var order = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid():N}",
            CustomerId = customerId,
            ShippingAddressId = Guid.NewGuid(),
            BillingAddressId = Guid.NewGuid(),
            Subtotal = total,
            Total = total,
            PlacedAt = placedAt
        };

        if (paymentStatus == PaymentStatus.Successful)
            order.MarkPaymentSuccessful();

        return order;
    }
}
