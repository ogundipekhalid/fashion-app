using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Wishlist.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Wishlist.Queries.GetMyWishlist;

public record GetMyWishlistQuery(Guid CustomerId) : IRequest<List<WishlistItemDto>>;

public class GetMyWishlistQueryHandler : IRequestHandler<GetMyWishlistQuery, List<WishlistItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyWishlistQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<WishlistItemDto>> Handle(GetMyWishlistQuery request, CancellationToken cancellationToken)
    {
        var items = await _context.WishlistItems
            .Where(w => w.CustomerId == request.CustomerId)
            .Include(w => w.Product).ThenInclude(p => p.Images)
            .Include(w => w.Product).ThenInclude(p => p.Variants)
            .Include(w => w.ProductVariant)
            .OrderByDescending(w => w.AddedAt)
            .ToListAsync(cancellationToken);

        return items.Select(w => new WishlistItemDto(
            w.Id, w.ProductId, w.Product.Name, w.Product.Slug,
            w.ProductVariantId, w.ProductVariant?.Color, w.ProductVariant?.Size,
            w.ProductVariant?.PriceOverride ?? w.Product.CurrentPrice,
            w.Product.Images.FirstOrDefault(i => i.IsPrimary)?.Url ?? w.Product.Images.FirstOrDefault()?.Url,
            w.ProductVariant?.IsInStock ?? w.Product.Variants.Any(v => v.IsInStock),
            w.AddedAt
        )).ToList();
    }
}
