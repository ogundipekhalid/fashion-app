using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Payments.Commands.ConfirmPayment;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Entities.Orders;
using FashionCommerce.Domain.Entities.Payments;
using FashionCommerce.Domain.Entities.Payouts;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Moq;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class ConfirmPaymentCommandTests
{
    private static Mock<IPaymentGatewayFactory> MockGatewayReturning(VerifyPaymentResult verifyResult)
    {
        var gateway = new Mock<IPaymentGateway>();
        gateway.Setup(g => g.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(verifyResult);

        var factory = new Mock<IPaymentGatewayFactory>();
        factory.Setup(f => f.Resolve(It.IsAny<PaymentProvider>())).Returns(gateway.Object);
        return factory;
    }

    [Fact]
    public async Task Confirming_a_paid_transaction_marks_the_order_Paid_and_credits_the_vendor_wallet()
    {
        await using var context = TestDbContextFactory.Create();
        var (order, payment, wallet) = await SeedPendingOrderWithPaymentAsync(context, unitPrice: 100m, quantity: 2, commissionRate: 20m);

        var factory = MockGatewayReturning(VerifyPaymentResult.Paid(200m, "NGN", "{}"));
        var handler = new ConfirmPaymentCommandHandler(context, factory.Object);

        var result = await handler.Handle(new ConfirmPaymentCommand(payment.ProviderReference), CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var reloadedOrder = context.Orders.Local.Single(o => o.Id == order.Id);
        reloadedOrder.Status.Should().Be(OrderStatus.Paid);
        reloadedOrder.PaymentStatus.Should().Be(PaymentStatus.Successful);

        var reloadedWallet = context.VendorWallets.Local.Single(w => w.Id == wallet.Id);
        reloadedWallet.PendingBalance.Should().Be(160m); // 200 - 20% commission

        context.CommissionTransactions.Local.Should().ContainSingle(t => t.VendorEarning == 160m);
    }

    [Fact]
    public async Task Confirming_an_unpaid_transaction_marks_the_order_Failed_and_credits_nothing()
    {
        await using var context = TestDbContextFactory.Create();
        var (order, payment, wallet) = await SeedPendingOrderWithPaymentAsync(context, unitPrice: 100m, quantity: 1, commissionRate: 20m);

        var factory = MockGatewayReturning(VerifyPaymentResult.NotPaid("{}"));
        var handler = new ConfirmPaymentCommandHandler(context, factory.Object);

        var result = await handler.Handle(new ConfirmPaymentCommand(payment.ProviderReference), CancellationToken.None);

        result.Succeeded.Should().BeFalse();

        var reloadedOrder = context.Orders.Local.Single(o => o.Id == order.Id);
        reloadedOrder.Status.Should().Be(OrderStatus.Failed);

        var reloadedWallet = context.VendorWallets.Local.Single(w => w.Id == wallet.Id);
        reloadedWallet.PendingBalance.Should().Be(0m);
    }

    [Fact]
    public async Task An_amount_mismatch_between_the_order_and_the_provider_fails_and_flags_the_payment_without_crediting_anyone()
    {
        await using var context = TestDbContextFactory.Create();
        var (order, payment, wallet) = await SeedPendingOrderWithPaymentAsync(context, unitPrice: 100m, quantity: 2, commissionRate: 20m);

        // Provider reports a different amount than what the order expects (200) — should be caught, not silently trusted.
        var factory = MockGatewayReturning(VerifyPaymentResult.Paid(50m, "NGN", "{}"));
        var handler = new ConfirmPaymentCommandHandler(context, factory.Object);

        var result = await handler.Handle(new ConfirmPaymentCommand(payment.ProviderReference), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("does not match"));

        var reloadedWallet = context.VendorWallets.Local.Single(w => w.Id == wallet.Id);
        reloadedWallet.PendingBalance.Should().Be(0m); // never credited
    }

    [Fact]
    public async Task Confirming_an_already_successful_payment_a_second_time_is_a_safe_no_op_idempotent_call()
    {
        await using var context = TestDbContextFactory.Create();
        var (_, payment, wallet) = await SeedPendingOrderWithPaymentAsync(context, unitPrice: 100m, quantity: 1, commissionRate: 20m);

        var factory = MockGatewayReturning(VerifyPaymentResult.Paid(100m, "NGN", "{}"));
        var handler = new ConfirmPaymentCommandHandler(context, factory.Object);

        await handler.Handle(new ConfirmPaymentCommand(payment.ProviderReference), CancellationToken.None);
        var secondResult = await handler.Handle(new ConfirmPaymentCommand(payment.ProviderReference), CancellationToken.None);

        secondResult.Succeeded.Should().BeTrue();

        var reloadedWallet = context.VendorWallets.Local.Single(w => w.Id == wallet.Id);
        reloadedWallet.PendingBalance.Should().Be(80m); // credited exactly once, not twice
    }

    private static async Task<(Order Order, Payment Payment, VendorWallet Wallet)> SeedPendingOrderWithPaymentAsync(
        TestApplicationDbContext context, decimal unitPrice, int quantity, decimal commissionRate)
    {
        var customer = new Customer { IdentityUserId = Guid.NewGuid(), FirstName = "Test", LastName = "Customer", Email = "customer@test.dev" };
        var vendor = new Vendor { IdentityUserId = Guid.NewGuid(), BusinessName = "Test Vendor", ContactEmail = "vendor@test.dev", CommissionRatePercent = commissionRate };
        var wallet = new VendorWallet { VendorId = vendor.Id, Vendor = vendor };
        var category = new Category { Name = "Shirts", Slug = $"shirts-{Guid.NewGuid():N}" };
        var product = new Product
        {
            VendorId = vendor.Id, Vendor = vendor, CategoryId = category.Id, Category = category,
            Name = "Test Shirt", Slug = $"shirt-{Guid.NewGuid():N}", Description = "desc",
            Sku = $"SKU-{Guid.NewGuid():N}", BasePrice = unitPrice, Status = ProductStatus.Active
        };
        var variant = new ProductVariant
        {
            ProductId = product.Id, Product = product, Color = "Blue", Size = "L",
            Sku = $"SKU-VAR-{Guid.NewGuid():N}", StockQuantity = 50, IsActive = true
        };

        var order = new Order
        {
            OrderNumber = $"ORD-TEST-{Guid.NewGuid():N}",
            CustomerId = customer.Id,
            ShippingAddressId = Guid.NewGuid(),
            BillingAddressId = Guid.NewGuid(),
            Subtotal = unitPrice * quantity,
            Total = unitPrice * quantity
        };

        var orderItem = new OrderItem
        {
            OrderId = order.Id,
            VendorId = vendor.Id,
            ProductVariantId = variant.Id,
            ProductNameSnapshot = product.Name,
            ColorSnapshot = variant.Color,
            SizeSnapshot = variant.Size,
            Quantity = quantity,
            UnitPrice = unitPrice,
            CommissionRatePercent = commissionRate
        };
        orderItem.ComputeCommissionSplit();
        order.Items.Add(orderItem);

        var payment = new Payment
        {
            OrderId = order.Id,
            Order = order,
            Provider = PaymentProvider.Paystack,
            ProviderReference = $"ref-{Guid.NewGuid():N}",
            Amount = order.Total,
            Currency = "NGN",
            Status = PaymentStatus.Pending
        };

        context.Customers.Add(customer);
        context.Vendors.Add(vendor);
        context.VendorWallets.Add(wallet);
        context.Categories.Add(category);
        context.Products.Add(product);
        context.ProductVariants.Add(variant);
        context.Orders.Add(order);
        context.Payments.Add(payment);
        await context.SaveChangesAsync(CancellationToken.None);

        return (order, payment, wallet);
    }
}
