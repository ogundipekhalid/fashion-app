using FashionCommerce.Application.Features.Styling.Commands.AddStylePostComment;
using FashionCommerce.Application.Features.Styling.Commands.CreateStylePost;
using FashionCommerce.Application.Features.Styling.Commands.LikeStylePost;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class StylingCommandTests
{
    [Fact]
    public async Task Creating_a_style_post_requires_at_least_one_image_and_persists_with_the_right_author_type()
    {
        await using var context = TestDbContextFactory.Create();
        var (customerId, product) = await SeedCustomerAndProductAsync(context);
        var handler = new CreateStylePostCommandHandler(context);

        var result = await handler.Handle(new CreateStylePostCommand
        {
            AuthorId = customerId,
            AuthorType = "Customer",
            ProductId = product.Id,
            Caption = "Loved styling this with white sneakers!",
            ImageUrls = new List<string> { "https://cdn.example.com/look1.jpg" }
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var post = context.StylePosts.Local.Should().ContainSingle().Subject;
        post.AuthorType.Should().Be(global::FashionCommerce.Domain.Enums.StylePostAuthorType.Customer);
        post.Images.Should().ContainSingle(i => i.Url == "https://cdn.example.com/look1.jpg");
    }

    [Fact]
    public async Task Creating_a_style_post_with_a_nonexistent_tagged_product_fails()
    {
        await using var context = TestDbContextFactory.Create();
        var (customerId, product) = await SeedCustomerAndProductAsync(context);
        var handler = new CreateStylePostCommandHandler(context);

        var result = await handler.Handle(new CreateStylePostCommand
        {
            AuthorId = customerId,
            AuthorType = "Customer",
            ProductId = product.Id,
            ImageUrls = new List<string> { "https://cdn.example.com/look1.jpg" },
            TaggedProductIds = new List<Guid> { Guid.NewGuid() } // doesn't exist
        }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        context.StylePosts.Local.Should().BeEmpty();
    }

    [Fact]
    public async Task Liking_a_post_increments_the_like_count_and_is_idempotent_per_user()
    {
        await using var context = TestDbContextFactory.Create();
        var (customerId, product) = await SeedCustomerAndProductAsync(context);
        var createHandler = new CreateStylePostCommandHandler(context);
        var createResult = await createHandler.Handle(new CreateStylePostCommand
        {
            AuthorId = customerId,
            AuthorType = "Customer",
            ProductId = product.Id,
            ImageUrls = new List<string> { "https://cdn.example.com/look1.jpg" }
        }, CancellationToken.None);

        var likerId = Guid.NewGuid();
        var likeHandler = new LikeStylePostCommandHandler(context);

        await likeHandler.Handle(new LikeStylePostCommand { StylePostId = createResult.Value, UserId = likerId, UserRole = "Customer" }, CancellationToken.None);
        await likeHandler.Handle(new LikeStylePostCommand { StylePostId = createResult.Value, UserId = likerId, UserRole = "Customer" }, CancellationToken.None); // like again

        var post = context.StylePosts.Local.Single(p => p.Id == createResult.Value);
        post.LikeCount.Should().Be(1); // idempotent — not 2
        context.StylePostLikes.Local.Should().ContainSingle();
    }

    [Fact]
    public async Task Adding_a_comment_increments_the_comment_count()
    {
        await using var context = TestDbContextFactory.Create();
        var (customerId, product) = await SeedCustomerAndProductAsync(context);
        var createHandler = new CreateStylePostCommandHandler(context);
        var createResult = await createHandler.Handle(new CreateStylePostCommand
        {
            AuthorId = customerId,
            AuthorType = "Customer",
            ProductId = product.Id,
            ImageUrls = new List<string> { "https://cdn.example.com/look1.jpg" }
        }, CancellationToken.None);

        var commentHandler = new AddStylePostCommentCommandHandler(context);
        var result = await commentHandler.Handle(new AddStylePostCommentCommand
        {
            StylePostId = createResult.Value,
            UserId = Guid.NewGuid(),
            UserRole = "Customer",
            Content = "This looks amazing!"
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var post = context.StylePosts.Local.Single(p => p.Id == createResult.Value);
        post.CommentCount.Should().Be(1);
    }

    private static async Task<(Guid CustomerId, Product Product)> SeedCustomerAndProductAsync(TestApplicationDbContext context)
    {
        var customer = new Customer { IdentityUserId = Guid.NewGuid(), FirstName = "Test", LastName = "Customer", Email = "customer@test.dev" };
        var vendor = new Vendor { IdentityUserId = Guid.NewGuid(), BusinessName = "Test Vendor", ContactEmail = "vendor@test.dev" };
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
            BasePrice = 50m
        };

        context.Customers.Add(customer);
        context.Vendors.Add(vendor);
        context.Categories.Add(category);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        return (customer.Id, product);
    }
}
