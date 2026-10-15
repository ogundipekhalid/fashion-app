using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Styling.DTOs;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Styling.Queries.GetStylePostsForProduct;

public record GetStylePostsForProductQuery : IRequest<PaginatedList<StylePostDto>>
{
    public Guid ProductId { get; init; }

    /// <summary>Optional — if provided, LikedByRequester is computed accurately; otherwise it's always false (e.g. for an anonymous browsing session).</summary>
    public Guid? RequesterId { get; init; }

    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetStylePostsForProductQueryHandler : IRequestHandler<GetStylePostsForProductQuery, PaginatedList<StylePostDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStylePostsForProductQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<StylePostDto>> Handle(GetStylePostsForProductQuery request, CancellationToken cancellationToken)
    {
        var query = _context.StylePosts
            .Where(p => p.ProductId == request.ProductId && p.Status == StylePostStatus.Published)
            .Include(p => p.Product)
            .Include(p => p.Images)
            .Include(p => p.TaggedProducts).ThenInclude(t => t.Product)
            .Include(p => p.Likes)
            .OrderByDescending(p => p.CreatedAt);

        var page = await PaginatedList<Domain.Entities.Styling.StylePost>.CreateAsync(query, request.PageNumber, request.PageSize);

        // Author display names live on Customer/Vendor, resolved in a second batched query rather than N+1-ing per post.
        var customerAuthorIds = page.Items.Where(p => p.AuthorType == StylePostAuthorType.Customer).Select(p => p.AuthorId).ToList();
        var vendorAuthorIds = page.Items.Where(p => p.AuthorType == StylePostAuthorType.Vendor).Select(p => p.AuthorId).ToList();

        var customerNames = await _context.Customers
            .Where(c => customerAuthorIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => (c.FullName, c.ProfileImageUrl), cancellationToken);

        var vendorNames = await _context.Vendors
            .Where(v => vendorAuthorIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => (v.BusinessName, v.LogoUrl), cancellationToken);

        var dtos = page.Items.Select(p =>
        {
            var (displayName, imageUrl) = p.AuthorType == StylePostAuthorType.Customer
                ? customerNames.TryGetValue(p.AuthorId, out var c) ? c : ("Customer", (string?)null)
                : vendorNames.TryGetValue(p.AuthorId, out var v) ? v : ("Vendor", (string?)null);

            return new StylePostDto(
                p.Id, p.AuthorId, p.AuthorType.ToString(), displayName, imageUrl,
                p.ProductId, p.Product.Name,
                p.Caption,
                p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).ToList(),
                p.TaggedProducts.Select(t => new TaggedProductDto(t.ProductId, t.Product.Name)).ToList(),
                p.LikeCount,
                request.RequesterId.HasValue && p.Likes.Any(l => l.UserId == request.RequesterId.Value),
                p.CommentCount,
                p.CreatedAt);
        }).ToList();

        return new PaginatedList<StylePostDto>(dtos, page.TotalCount, page.PageNumber, request.PageSize);
    }
}
