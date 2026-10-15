using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Refunds.Commands.ProcessRefund;
using FashionCommerce.Application.Features.Refunds.Commands.RequestRefund;
using FashionCommerce.Application.Features.Refunds.Commands.VendorRespondToRefund;
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

public class RefundFlowTests
{
    [Fact]
    public async Task Full_refund_flow_customer_request_then_vendor_approval_then_processing_claws_back_the_wallet()
    {
        await using var context = TestDbContextFactory.Create();

        // Vendor's earnings on this item are still Pending (not yet released/paid out),
        // so this exercises the pre-payout refund path.
        var (order, orderItem, vendor, wallet, payment) = await SeedDeliveredPaidOrderAsync(
            context, unitPrice: 1000m, commissionRate: 20m, releaseToAvailable: false);

        // Step 1: Customer requests a refund.
        var requestHandler = new RequestRefundCommandHandler(context);
        var requestResult = await requestHandler.Handle(new RequestRefundCommand
        {
            OrderItemId = orderItem.Id,
            RequestingCustomerId = order.CustomerId,
            RequestedAmount = 1000m,
            Reason = "Item arrived damaged"
        }, CancellationToken.None);

        requestResult.Succeeded.Should().BeTrue();
        var refund = context.RefundRequests.Local.Should().ContainSingle().Subject;
        refund.WasVendorAlreadyPaidOut.Should().BeFalse();

        // Step 2: Vendor approves.
        var vendorRespondHandler = new VendorRespondToRefundCommandHandler(context);
        var vendorResult = await vendorRespondHandler.Handle(new VendorRespondToRefundCommand
        {
            RefundRequestId = refund.Id,
            RequestingVendorId = vendor.Id,
            Approved = true,
            Note = "Confirmed, approving refund."
        }, CancellationToken.None);

        vendorResult.Succeeded.Should().BeTrue();
        refund.Status.Should().Be(RefundStatus.ApprovedByVendor);

        // Step 3: Processing — calls the payment gateway and applies the wallet clawback.
        var gateway = new Mock<IPaymentGateway>();
        gateway.Setup(g => g.RefundAsync(payment.ProviderReference, 1000m, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RefundPaymentResult.Success("provider-refund-id"));
        var factory = new Mock<IPaymentGatewayFactory>();
        factory.Setup(f => f.Resolve(PaymentProvider.Paystack)).Returns(gateway.Object);

        var processHandler = new ProcessRefundCommandHandler(context, factory.Object);
        var processResult = await processHandler.Handle(new ProcessRefundCommand(refund.Id), CancellationToken.None);

        processResult.Succeeded.Should().BeTrue();
        refund.Status.Should().Be(RefundStatus.Refunded);

        var reloadedOrderItem = context.OrderItems.Local.Single(i => i.Id == orderItem.Id);
        reloadedOrderItem.Status.Should().Be(OrderItemStatus.Refunded);

        // Pre-payout path: the vendor's earning comes off the Pending balance, not clawed back from Available.
        var reloadedWallet = context.VendorWallets.Local.Single(w => w.VendorId == vendor.Id);
        reloadedWallet.PendingBalance.Should().Be(0m); // 800 pending - 800 refunded vendor share
        reloadedWallet.RefundBalance.Should().Be(800m);
    }

    [Fact]
    public async Task Refund_after_vendor_payout_claws_back_from_available_balance_and_can_go_negative()
    {
        await using var context = TestDbContextFactory.Create();

        // This time the vendor's earnings were already released AND withdrawn (paid out).
        var (order, orderItem, vendor, wallet, payment) = await SeedDeliveredPaidOrderAsync(
            context, unitPrice: 500m, commissionRate: 20m, releaseToAvailable: true);

        wallet.DebitForPayout(400m); // vendor already withdrew the full 400 (500 - 20%) earning
        await context.SaveChangesAsync(CancellationToken.None);

        var refund = FashionCommerce.Domain.Entities.Refunds.RefundRequest.Create(
            orderItem.Id, order.Id, order.CustomerId, vendor.Id, RefundType.Full, 500m, "Wrong size sent", vendorAlreadyPaidOut: true);
        refund.SubmitVendorResponse(approved: true, note: "Approved");
        context.RefundRequests.Add(refund);
        await context.SaveChangesAsync(CancellationToken.None);

        var gateway = new Mock<IPaymentGateway>();
        gateway.Setup(g => g.RefundAsync(payment.ProviderReference, 500m, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RefundPaymentResult.Success("provider-refund-id"));
        var factory = new Mock<IPaymentGatewayFactory>();
        factory.Setup(f => f.Resolve(PaymentProvider.Paystack)).Returns(gateway.Object);

        var processHandler = new ProcessRefundCommandHandler(context, factory.Object);
        var result = await processHandler.Handle(new ProcessRefundCommand(refund.Id), CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var reloadedWallet = context.VendorWallets.Local.Single(w => w.VendorId == vendor.Id);
        reloadedWallet.AvailableBalance.Should().Be(-400m); // clawed back even though it drives the balance negative
    }

    private static async Task<(Order Order, OrderItem Item, Vendor Vendor, VendorWallet Wallet, Payment Payment)> SeedDeliveredPaidOrderAsync(
        TestApplicationDbContext context, decimal unitPrice, decimal commissionRate, bool releaseToAvailable)
    {
        var customer = new Customer { IdentityUserId = Guid.NewGuid(), FirstName = "Test", LastName = "Customer", Email = "customer@test.dev" };
        var vendor = new Vendor { IdentityUserId = Guid.NewGuid(), BusinessName = "Test Vendor", ContactEmail = "vendor@test.dev", CommissionRatePercent = commissionRate };
        var wallet = new VendorWallet { VendorId = vendor.Id, Vendor = vendor };
        var category = new Category { Name = "Shirts", Slug = $"shirts-{Guid.NewGuid():N}" };
        var product = new Product
        {
            VendorId = vendor.Id, Vendor = vendor, CategoryId = category.Id, Category = category,
            Name = "Test Shirt", Slug = $"shirt-{Guid.NewGuid():N}", Description = "desc",
            Sku = $"SKU-{Guid.NewGuid():N}", BasePrice = unitPrice
        };
        var variant = new ProductVariant
        {
            ProductId = product.Id, Product = product, Color = "Blue", Size = "L",
            Sku = $"SKU-VAR-{Guid.NewGuid():N}", StockQuantity = 10
        };

        var order = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid():N}", CustomerId = customer.Id,
            ShippingAddressId = Guid.NewGuid(), BillingAddressId = Guid.NewGuid(),
            Subtotal = unitPrice, Total = unitPrice
        };
        var orderItem = new OrderItem
        {
            OrderId = order.Id, VendorId = vendor.Id, ProductVariantId = variant.Id,
            ProductNameSnapshot = product.Name, ColorSnapshot = variant.Color, SizeSnapshot = variant.Size,
            Quantity = 1, UnitPrice = unitPrice, CommissionRatePercent = commissionRate,
            Status = OrderItemStatus.Delivered
        };
        orderItem.ComputeCommissionSplit();
        order.Items.Add(orderItem);
        order.MarkPaymentSuccessful();

        var payment = new Payment
        {
            OrderId = order.Id, Order = order, Provider = PaymentProvider.Paystack,
            ProviderReference = $"ref-{Guid.NewGuid():N}", Amount = unitPrice, Status = PaymentStatus.Successful
        };

        wallet.CreditPending(orderItem.VendorEarning);
        if (releaseToAvailable) wallet.ReleaseToAvailable(orderItem.VendorEarning);

        context.Customers.Add(customer);
        context.Vendors.Add(vendor);
        context.VendorWallets.Add(wallet);
        context.Categories.Add(category);
        context.Products.Add(product);
        context.ProductVariants.Add(variant);
        context.Orders.Add(order);
        context.Payments.Add(payment);
        await context.SaveChangesAsync(CancellationToken.None);

        return (order, orderItem, vendor, wallet, payment);
    }
}
