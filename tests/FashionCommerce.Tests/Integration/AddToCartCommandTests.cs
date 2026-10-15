using FashionCommerce.Application.Features.Cart.Commands.AddToCart;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class AddToCartCommandTests
{
    [Fact]
    public async Task Adding_a_new_item_creates_a_cart_and_a_cart_item()
    {
        await using var context = TestDbContextFactory.Create();

        var (product, variant) = await SeedActiveProductWithVariantAsync(context, stock: 10);
        var customerId = Guid.NewGuid();

        var handler = new AddToCartCommandHandler(context);
        var result = await handler.Handle(new AddToCartCommand
        {
            CustomerId = customerId,
            ProductVariantId = variant.Id,
            Quantity = 2
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var cart = context.Carts.Local.Should().ContainSingle().Subject;
        cart.CustomerId.Should().Be(customerId);
        cart.Items.Should().ContainSingle(i => i.ProductVariantId == variant.Id && i.Quantity == 2);
    }

    [Fact]
    public async Task Adding_the_same_variant_twice_combines_quantity_rather_than_creating_a_second_line()
    {
        await using var context = TestDbContextFactory.Create();
        var (_, variant) = await SeedActiveProductWithVariantAsync(context, stock: 10);
        var customerId = Guid.NewGuid();
        var handler = new AddToCartCommandHandler(context);

        await handler.Handle(new AddToCartCommand { CustomerId = customerId, ProductVariantId = variant.Id, Quantity = 2 }, CancellationToken.None);
        await handler.Handle(new AddToCartCommand { CustomerId = customerId, ProductVariantId = variant.Id, Quantity = 3 }, CancellationToken.None);

        var cart = context.Carts.Local.Should().ContainSingle().Subject;
        cart.Items.Should().ContainSingle().Which.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task Requesting_more_than_available_stock_fails_without_writing_to_the_cart()
    {
        await using var context = TestDbContextFactory.Create();
        var (_, variant) = await SeedActiveProductWithVariantAsync(context, stock: 2);
        var handler = new AddToCartCommandHandler(context);

        var result = await handler.Handle(new AddToCartCommand
        {
            CustomerId = Guid.NewGuid(),
            ProductVariantId = variant.Id,
            Quantity = 5
        }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        context.Carts.Local.Should().BeEmpty();
    }

    [Fact]
    public async Task Guest_and_authenticated_carts_are_kept_separate()
    {
        await using var context = TestDbContextFactory.Create();
        var (_, variant) = await SeedActiveProductWithVariantAsync(context, stock: 10);
        var handler = new AddToCartCommandHandler(context);

        await handler.Handle(new AddToCartCommand { GuestSessionId = "guest-session-1", ProductVariantId = variant.Id, Quantity = 1 }, CancellationToken.None);
        await handler.Handle(new AddToCartCommand { CustomerId = Guid.NewGuid(), ProductVariantId = variant.Id, Quantity = 1 }, CancellationToken.None);

        context.Carts.Local.Should().HaveCount(2);
    }

    private static async Task<(Product Product, ProductVariant Variant)> SeedActiveProductWithVariantAsync(TestApplicationDbContext context, int stock)
    {
        var vendor = new Vendor
        {
            IdentityUserId = Guid.NewGuid(),
            BusinessName = "Test Vendor",
            ContactEmail = "vendor@test.dev",
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
            Slug = $"test-shirt-{Guid.NewGuid():N}",
            Description = "A shirt",
            Sku = $"SKU-{Guid.NewGuid():N}",
            BasePrice = 20m,
            Status = ProductStatus.Active
        };

        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Product = product,
            Color = "Red",
            Size = "M",
            Sku = $"SKU-VAR-{Guid.NewGuid():N}",
            StockQuantity = stock,
            IsActive = true
        };

        context.Vendors.Add(vendor);
        context.Categories.Add(category);
        context.Products.Add(product);
        context.ProductVariants.Add(variant);
        await context.SaveChangesAsync(CancellationToken.None);

        return (product, variant);
    }
}
