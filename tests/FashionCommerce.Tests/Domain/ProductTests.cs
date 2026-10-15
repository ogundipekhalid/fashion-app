using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Domain;

public class ProductTests
{
    private static Product CreateProduct() => new()
    {
        VendorId = Guid.NewGuid(),
        CategoryId = Guid.NewGuid(),
        Name = "Test Shirt",
        Slug = "test-shirt",
        Description = "A shirt for testing",
        Sku = "SKU-001",
        BasePrice = 50m,
        Status = ProductStatus.PendingApproval
    };

    [Fact]
    public void RecalculateRating_averages_non_deleted_reviews()
    {
        var product = CreateProduct();
        var reviews = new List<ProductReview>
        {
            ProductReview.Create(product.Id, Guid.NewGuid(), 5, null, null, null),
            ProductReview.Create(product.Id, Guid.NewGuid(), 3, null, null, null),
            ProductReview.Create(product.Id, Guid.NewGuid(), 4, null, null, null)
        };

        product.RecalculateRating(reviews);

        product.AverageRating.Should().Be(4.00m);
        product.ReviewCount.Should().Be(3);
    }

    [Fact]
    public void RecalculateRating_excludes_soft_deleted_reviews()
    {
        var product = CreateProduct();
        var deletedReview = ProductReview.Create(product.Id, Guid.NewGuid(), 1, null, null, null);
        deletedReview.IsDeleted = true;

        var reviews = new List<ProductReview>
        {
            ProductReview.Create(product.Id, Guid.NewGuid(), 5, null, null, null),
            deletedReview
        };

        product.RecalculateRating(reviews);

        product.AverageRating.Should().Be(5.00m);
        product.ReviewCount.Should().Be(1);
    }

    [Fact]
    public void RecalculateRating_with_no_reviews_resets_to_zero()
    {
        var product = CreateProduct();

        product.RecalculateRating(Enumerable.Empty<ProductReview>());

        product.AverageRating.Should().Be(0);
        product.ReviewCount.Should().Be(0);
    }

    [Fact]
    public void Approve_sets_status_Active_and_clears_rejection_reason()
    {
        var product = CreateProduct();
        product.Reject("needs better photos");

        product.Approve();

        product.Status.Should().Be(ProductStatus.Active);
        product.RejectionReason.Should().BeNull();
        product.DomainEvents.Should().Contain(e => e.GetType().Name == "ProductApprovedEvent");
    }

    [Fact]
    public void Reject_sets_status_Rejected_with_reason()
    {
        var product = CreateProduct();

        product.Reject("Prohibited item");

        product.Status.Should().Be(ProductStatus.Rejected);
        product.RejectionReason.Should().Be("Prohibited item");
        product.DomainEvents.Should().Contain(e => e.GetType().Name == "ProductRejectedEvent");
    }

    [Fact]
    public void CurrentPrice_uses_sale_price_when_lower_than_base_price()
    {
        var product = CreateProduct();
        product.BasePrice = 100m;
        product.SalePrice = 75m;

        product.CurrentPrice.Should().Be(75m);
    }

    [Fact]
    public void CurrentPrice_falls_back_to_base_price_when_sale_price_is_not_lower()
    {
        var product = CreateProduct();
        product.BasePrice = 100m;
        product.SalePrice = 120m; // invalid/stale sale price higher than base

        product.CurrentPrice.Should().Be(100m);
    }
}
