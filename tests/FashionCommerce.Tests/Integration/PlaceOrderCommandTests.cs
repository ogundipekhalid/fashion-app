using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Orders.Commands.PlaceOrder;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Moq;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class PlaceOrderCommandTests
{
    private static (Mock<IPaymentGatewayFactory> Factory, Mock<IPaymentGateway> Gateway) MockSuccessfulGateway()
    {
        var gateway = new Mock<IPaymentGateway>();
        gateway.Setup(g => g.InitializeAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InitializePaymentResult.Success("https://checkout.example.com/pay", "ref-123"));

        var factory = new Mock<IPaymentGatewayFactory>();
        factory.Setup(f => f.Resolve(It.IsAny<PaymentProvider>())).Returns(gateway.Object);

        return (factory, gateway);
    }

    [Fact]
    public async Task Placing_an_order_creates_the_order_with_correct_commission_split_and_reserves_stock()
    {
        await using var context = TestDbContextFactory.Create();
        var (customer, address, vendor, product, variant) = await SeedCheckoutFixturesAsync(context, stock: 10, vendorCommissionRate: 20m);

        var cart = new global::FashionCommerce.Domain.Entities.Cart.Cart { CustomerId = customer.Id };
        cart.Items.Add(new global::FashionCommerce.Domain.Entities.Cart.CartItem { CartId = cart.Id, ProductVariantId = variant.Id, Quantity = 2, PriceAtAdd = 100m });
        context.Carts.Add(cart);
        await context.SaveChangesAsync(CancellationToken.None);

        var (factoryMock, _) = MockSuccessfulGateway();
        var handler = new PlaceOrderCommandHandler(context, factoryMock.Object);

        var result = await handler.Handle(new PlaceOrderCommand
        {
            CustomerId = customer.Id,
            CartId = cart.Id,
            ShippingAddressId = address.Id,
            BillingAddressId = address.Id,
            PaymentProvider = "Paystack",
            CallbackUrl = "https://app.example.com/checkout/callback"
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var order = context.Orders.Local.Should().ContainSingle().Subject;
        order.Total.Should().Be(200m); // 2 * 100, no tax/shipping/discount configured
        order.Status.Should().Be(OrderStatus.PendingPayment); // not yet paid — that's ConfirmPaymentCommand's job

        var orderItem = order.Items.Should().ContainSingle().Subject;
        orderItem.CommissionAmount.Should().Be(40m);  // 20% of 200
        orderItem.VendorEarning.Should().Be(160m);

        var reloadedVariant = context.ProductVariants.Local.Single(v => v.Id == variant.Id);
        reloadedVariant.StockQuantity.Should().Be(8); // 10 - 2 reserved

        context.CartItems.Local.Should().BeEmpty(); // cart consumed on placement
    }

    [Fact]
    public async Task Placing_an_order_with_insufficient_stock_fails_without_creating_an_order()
    {
        await using var context = TestDbContextFactory.Create();
        var (customer, address, _, _, variant) = await SeedCheckoutFixturesAsync(context, stock: 1, vendorCommissionRate: 20m);

        var cart = new global::FashionCommerce.Domain.Entities.Cart.Cart { CustomerId = customer.Id };
        cart.Items.Add(new global::FashionCommerce.Domain.Entities.Cart.CartItem { CartId = cart.Id, ProductVariantId = variant.Id, Quantity = 5, PriceAtAdd = 100m });
        context.Carts.Add(cart);
        await context.SaveChangesAsync(CancellationToken.None);

        var (factoryMock, _) = MockSuccessfulGateway();
        var handler = new PlaceOrderCommandHandler(context, factoryMock.Object);

        var result = await handler.Handle(new PlaceOrderCommand
        {
            CustomerId = customer.Id,
            CartId = cart.Id,
            ShippingAddressId = address.Id,
            BillingAddressId = address.Id,
            PaymentProvider = "Paystack",
            CallbackUrl = "https://app.example.com/checkout/callback"
        }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        context.Orders.Local.Should().BeEmpty();
    }

    [Fact]
    public async Task Placing_an_order_with_an_empty_cart_fails_gracefully()
    {
        await using var context = TestDbContextFactory.Create();
        var (customer, address, _, _, _) = await SeedCheckoutFixturesAsync(context, stock: 10, vendorCommissionRate: 20m);

        var cart = new global::FashionCommerce.Domain.Entities.Cart.Cart { CustomerId = customer.Id };
        context.Carts.Add(cart);
        await context.SaveChangesAsync(CancellationToken.None);

        var (factoryMock, _) = MockSuccessfulGateway();
        var handler = new PlaceOrderCommandHandler(context, factoryMock.Object);

        var result = await handler.Handle(new PlaceOrderCommand
        {
            CustomerId = customer.Id,
            CartId = cart.Id,
            ShippingAddressId = address.Id,
            BillingAddressId = address.Id,
            PaymentProvider = "Paystack",
            CallbackUrl = "https://app.example.com/checkout/callback"
        }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("empty"));
    }

    private static async Task<(Customer Customer, Address Address, Vendor Vendor, Product Product, ProductVariant Variant)> SeedCheckoutFixturesAsync(
        TestApplicationDbContext context, int stock, decimal vendorCommissionRate)
    {
        var customer = new Customer { IdentityUserId = Guid.NewGuid(), FirstName = "Test", LastName = "Customer", Email = "customer@test.dev" };
        var address = new Address
        {
            CustomerId = customer.Id,
            Customer = customer,
            Label = "Home",
            RecipientName = "Test Customer",
            RecipientPhone = "08000000000",
            Street = "1 Test St",
            City = "Lagos",
            State = "Lagos",
            Country = "Nigeria"
        };
        var vendor = new Vendor
        {
            IdentityUserId = Guid.NewGuid(),
            BusinessName = "Test Vendor",
            ContactEmail = "vendor@test.dev",
            CommissionRatePercent = vendorCommissionRate,
            VerificationStatus = VendorVerificationStatus.Verified
        };
        var category = new Category { Name = "Shirts", Slug = $"shirts-{Guid.NewGuid():N}" };
        var product = new Product
        {
            VendorId = vendor.Id,
            Vendor = vendor,
            CategoryId = category.Id,
            Category = category,
            Name = "Test Shirt",
            Slug = $"shirt-{Guid.NewGuid():N}",
            Description = "desc",
            Sku = $"SKU-{Guid.NewGuid():N}",
            BasePrice = 100m,
            Status = ProductStatus.Active
        };
        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Product = product,
            Color = "Blue",
            Size = "L",
            Sku = $"SKU-VAR-{Guid.NewGuid():N}",
            StockQuantity = stock,
            IsActive = true
        };

        context.Customers.Add(customer);
        context.Addresses.Add(address);
        context.Vendors.Add(vendor);
        context.Categories.Add(category);
        context.Products.Add(product);
        context.ProductVariants.Add(variant);
        await context.SaveChangesAsync(CancellationToken.None);

        return (customer, address, vendor, product, variant);
    }
}
