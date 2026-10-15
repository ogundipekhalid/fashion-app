using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.AI.Commands.GenerateProductContent;
using FashionCommerce.Application.Features.AI.Commands.SummarizeReview;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Moq;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class AiContentGenerationTests
{
    [Fact]
    public async Task Generating_product_content_returns_the_AI_suggestion_without_writing_to_the_database()
    {
        var aiService = new Mock<IAiService>();
        aiService.Setup(s => s.GenerateProductContentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProductContentSuggestion.Success(
                "A breathable cotton shirt for warm days.",
                "Blue Cotton Shirt | FashionCommerce",
                "Shop our breathable blue cotton shirt, perfect for warm weather.",
                new List<string> { "cotton", "shirt", "casual", "summer", "blue" }));

        var handler = new GenerateProductContentCommandHandler(aiService.Object);

        var result = await handler.Handle(new GenerateProductContentCommand
        {
            ProductName = "Blue Cotton Shirt",
            Category = "Shirts",
            Material = "Cotton",
            TargetGender = "Male"
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Description.Should().Contain("cotton");
        result.Value.Tags.Should().HaveCount(5);

        // This command is deliberately a suggestion-only operation — the vendor
        // reviews and edits before it ever reaches CreateProduct. It takes no
        // DbContext at all, which is what guarantees nothing is auto-published.
    }

    [Fact]
    public async Task A_failing_AI_provider_surfaces_the_error_rather_than_returning_empty_content()
    {
        var aiService = new Mock<IAiService>();
        aiService.Setup(s => s.GenerateProductContentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProductContentSuggestion.Failure("Gemini returned 429: rate limit exceeded."));

        var handler = new GenerateProductContentCommandHandler(aiService.Object);

        var result = await handler.Handle(new GenerateProductContentCommand
        {
            ProductName = "Blue Cotton Shirt",
            Category = "Shirts"
        }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("rate limit"));
    }

    [Fact]
    public async Task Summarizing_a_review_persists_the_summary_onto_the_review()
    {
        await using var context = TestDbContextFactory.Create();
        var review = await SeedReviewAsync(context, comment: "Great fit and the fabric feels premium, though delivery took a week.");

        var aiService = new Mock<IAiService>();
        aiService.Setup(s => s.SummarizeReviewAsync(review.Comment!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiTextResult.Success("Great fit and premium fabric, but slow delivery."));

        var handler = new SummarizeReviewCommandHandler(context, aiService.Object);
        var result = await handler.Handle(new SummarizeReviewCommand(review.Id), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var reloaded = context.ProductReviews.Local.Single(r => r.Id == review.Id);
        reloaded.AiSummary.Should().Be("Great fit and premium fabric, but slow delivery.");
    }

    [Fact]
    public async Task Summarizing_a_review_with_no_comment_text_fails_without_calling_the_AI_provider()
    {
        await using var context = TestDbContextFactory.Create();
        var review = await SeedReviewAsync(context, comment: null); // rating-only review, no prose to summarize

        var aiService = new Mock<IAiService>();
        var handler = new SummarizeReviewCommandHandler(context, aiService.Object);

        var result = await handler.Handle(new SummarizeReviewCommand(review.Id), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        // Important for a metered/rate-limited free tier: don't spend an API call on something we already know can't work.
        aiService.Verify(s => s.SummarizeReviewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_failing_AI_call_leaves_the_existing_review_summary_untouched()
    {
        await using var context = TestDbContextFactory.Create();
        var review = await SeedReviewAsync(context, comment: "Decent quality for the price.");
        review.AiSummary = "Previously generated summary.";
        await context.SaveChangesAsync(CancellationToken.None);

        var aiService = new Mock<IAiService>();
        aiService.Setup(s => s.SummarizeReviewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiTextResult.Failure("Gemini returned an empty response."));

        var handler = new SummarizeReviewCommandHandler(context, aiService.Object);
        var result = await handler.Handle(new SummarizeReviewCommand(review.Id), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        var reloaded = context.ProductReviews.Local.Single(r => r.Id == review.Id);
        reloaded.AiSummary.Should().Be("Previously generated summary."); // not overwritten with null/garbage
    }

    [Fact]
    public async Task Summarizing_a_review_that_does_not_exist_throws_NotFound()
    {
        await using var context = TestDbContextFactory.Create();
        var handler = new SummarizeReviewCommandHandler(context, Mock.Of<IAiService>());

        var act = async () => await handler.Handle(new SummarizeReviewCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static async Task<ProductReview> SeedReviewAsync(TestApplicationDbContext context, string? comment)
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

        var review = ProductReview.Create(product.Id, customer.Id, rating: 4, title: "Good", comment: comment, orderItemId: null);

        context.Customers.Add(customer);
        context.Vendors.Add(vendor);
        context.Categories.Add(category);
        context.Products.Add(product);
        context.ProductReviews.Add(review);
        await context.SaveChangesAsync(CancellationToken.None);

        return review;
    }
}
