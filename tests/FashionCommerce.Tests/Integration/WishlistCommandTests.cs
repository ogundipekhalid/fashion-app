using FashionCommerce.Application.Features.Wishlist.Commands.AddToWishlist;
using FashionCommerce.Application.Features.Wishlist.Commands.RemoveFromWishlist;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class WishlistCommandTests
{
    [Fact]
    public async Task Adding_a_product_to_the_wishlist_creates_a_wishlist_item()
    {
        await using var context = TestDbContextFactory.Create();
        var (customerId, product) = await SeedCustomerAndProductAsync(context);
        var handler = new AddToWishlistCommandHandler(context);

        var result = await handler.Handle(new AddToWishlistCommand { CustomerId = customerId, ProductId = product.Id }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        context.WishlistItems.Local.Should().ContainSingle(w => w.CustomerId == customerId && w.ProductId == product.Id);
    }

    [Fact]
    public async Task Adding_the_same_product_twice_is_idempotent_and_does_not_duplicate()
    {
        await using var context = TestDbContextFactory.Create();
        var (customerId, product) = await SeedCustomerAndProductAsync(context);
        var handler = new AddToWishlistCommandHandler(context);

        var first = await handler.Handle(new AddToWishlistCommand { CustomerId = customerId, ProductId = product.Id }, CancellationToken.None);
        var second = await handler.Handle(new AddToWishlistCommand { CustomerId = customerId, ProductId = product.Id }, CancellationToken.None);

        first.Value.Should().Be(second.Value); // same wishlist item id returned both times
        context.WishlistItems.Local.Should().ContainSingle();
    }

    [Fact]
    public async Task Removing_a_wishlist_item_deletes_it()
    {
        await using var context = TestDbContextFactory.Create();
        var (customerId, product) = await SeedCustomerAndProductAsync(context);
        var addHandler = new AddToWishlistCommandHandler(context);
        var addResult = await addHandler.Handle(new AddToWishlistCommand { CustomerId = customerId, ProductId = product.Id }, CancellationToken.None);

        var removeHandler = new RemoveFromWishlistCommandHandler(context);
        var removeResult = await removeHandler.Handle(
            new RemoveFromWishlistCommand { WishlistItemId = addResult.Value, CustomerId = customerId }, CancellationToken.None);

        removeResult.Succeeded.Should().BeTrue();
        context.WishlistItems.Local.Should().BeEmpty();
    }

    [Fact]
    public async Task Removing_a_wishlist_item_belonging_to_a_different_customer_is_a_safe_no_op()
    {
        await using var context = TestDbContextFactory.Create();
        var (customerId, product) = await SeedCustomerAndProductAsync(context);
        var addHandler = new AddToWishlistCommandHandler(context);
        var addResult = await addHandler.Handle(new AddToWishlistCommand { CustomerId = customerId, ProductId = product.Id }, CancellationToken.None);

        var removeHandler = new RemoveFromWishlistCommandHandler(context);
        var removeResult = await removeHandler.Handle(
            new RemoveFromWishlistCommand { WishlistItemId = addResult.Value, CustomerId = Guid.NewGuid() }, CancellationToken.None);

        removeResult.Succeeded.Should().BeTrue(); // no error leaked about existence — just doesn't remove someone else's item
        context.WishlistItems.Local.Should().ContainSingle(); // still there, untouched
    }

    private static async Task<(Guid CustomerId, Product Product)> SeedCustomerAndProductAsync(FashionCommerce.Tests.Common.TestApplicationDbContext context)
    {
        var customer = new Customer { IdentityUserId = Guid.NewGuid(), FirstName = "Test", LastName = "Customer", Email = "customer@test.dev" };
        var vendor = new Vendor { IdentityUserId = Guid.NewGuid(), BusinessName = "Test Vendor", ContactEmail = "vendor@test.dev" };
        var category = new Category { Name = "Shirts", Slug = $"shirts-{Guid.NewGuid():N}" };
        var product = new Product
        {
            VendorId = vendor.Id, Vendor = vendor, CategoryId = category.Id, Category = category,
            Name = "Test Shirt", Slug = $"shirt-{Guid.NewGuid():N}", Description = "desc",
            Sku = $"SKU-{Guid.NewGuid():N}", BasePrice = 50m
        };

        context.Customers.Add(customer);
        context.Vendors.Add(vendor);
        context.Categories.Add(category);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        return (customer.Id, product);
    }
}
