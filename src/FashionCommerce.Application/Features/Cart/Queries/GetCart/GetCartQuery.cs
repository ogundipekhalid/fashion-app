using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Cart.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Cart.Queries.GetCart;

public record GetCartQuery(Guid? CustomerId, string? GuestSessionId) : IRequest<CartDto?>;

public class GetCartQueryHandler : IRequestHandler<GetCartQuery, CartDto?>
{
    private readonly IApplicationDbContext _context;

    public GetCartQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CartDto?> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Carts
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v.Product).ThenInclude(p => p.Images)
            .AsQueryable();

        var cart = request.CustomerId.HasValue
            ? await query.FirstOrDefaultAsync(c => c.CustomerId == request.CustomerId, cancellationToken)
            : await query.FirstOrDefaultAsync(c => c.GuestSessionId == request.GuestSessionId, cancellationToken);

        if (cart is null) return null;

        var items = cart.Items.Select(i => new CartItemDto(
            i.Id, i.ProductVariantId, i.ProductVariant.ProductId, i.ProductVariant.Product.Name,
            i.ProductVariant.Color, i.ProductVariant.Size,
            i.ProductVariant.Images.FirstOrDefault()?.Url ?? i.ProductVariant.Product.Images.FirstOrDefault(img => img.IsPrimary)?.Url,
            i.PriceAtAdd, i.Quantity, i.LineTotal,
            i.ProductVariant.IsInStock, i.ProductVariant.StockQuantity
        )).ToList();

        decimal discount = 0;
        if (!string.IsNullOrWhiteSpace(cart.AppliedCouponCode))
        {
            var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == cart.AppliedCouponCode, cancellationToken);
            if (coupon is not null && coupon.IsCurrentlyValid(DateTime.UtcNow))
            {
                try { discount = coupon.CalculateDiscount(cart.Subtotal); }
                catch (Domain.Exceptions.InvalidCouponException) { discount = 0; }
            }
        }

        return new CartDto(cart.Id, items, cart.Subtotal, cart.AppliedCouponCode, discount, Math.Max(0, cart.Subtotal - discount));
    }
}
