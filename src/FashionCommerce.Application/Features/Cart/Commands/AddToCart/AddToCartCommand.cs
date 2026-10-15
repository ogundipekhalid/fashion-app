using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Cart;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Cart.Commands.AddToCart;

public record AddToCartCommand : IRequest<Result<Guid>>
{
    /// <summary>Resolved server-side when authenticated; null for a guest request.</summary>
    public Guid? CustomerId { get; init; }

    /// <summary>Required when CustomerId is null — a client-generated identifier persisted in a cookie/localStorage.</summary>
    public string? GuestSessionId { get; init; }

    public Guid ProductVariantId { get; init; }
    public int Quantity { get; init; } = 1;
}

public class AddToCartCommandValidator : AbstractValidator<AddToCartCommand>
{
    public AddToCartCommandValidator()
    {
        RuleFor(x => x.ProductVariantId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(50);
        RuleFor(x => x)
            .Must(x => x.CustomerId.HasValue || !string.IsNullOrWhiteSpace(x.GuestSessionId))
            .WithMessage("Either an authenticated customer or a guest session id is required.");
    }
}

public class AddToCartCommandHandler : IRequestHandler<AddToCartCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public AddToCartCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(AddToCartCommand request, CancellationToken cancellationToken)
    {
        var variant = await _context.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == request.ProductVariantId, cancellationToken);

        if (variant is null)
            throw new NotFoundException(nameof(Domain.Entities.Catalog.ProductVariant), request.ProductVariantId);

        if (!variant.IsActive || variant.Product.Status != Domain.Enums.ProductStatus.Active)
            return Result<Guid>.Failure("This product is not currently available.");

        if (variant.StockQuantity < request.Quantity)
            return Result<Guid>.Failure($"Only {variant.StockQuantity} unit(s) left in stock.");

        var cart = await GetOrCreateCartAsync(request.CustomerId, request.GuestSessionId, cancellationToken);

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductVariantId == request.ProductVariantId);
        var effectivePrice = variant.PriceOverride ?? variant.Product.CurrentPrice;

        if (existingItem is not null)
        {
            var newQuantity = existingItem.Quantity + request.Quantity;
            if (variant.StockQuantity < newQuantity)
                return Result<Guid>.Failure($"Only {variant.StockQuantity} unit(s) left in stock.");

            existingItem.Quantity = newQuantity;
            existingItem.PriceAtAdd = effectivePrice; // refresh to current price on re-add
        }
        else
        {
            cart.Items.Add(new CartItem
            {
                CartId = cart.Id,
                ProductVariantId = request.ProductVariantId,
                Quantity = request.Quantity,
                PriceAtAdd = effectivePrice
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(cart.Id);
    }

    private async Task<Domain.Entities.Cart.Cart> GetOrCreateCartAsync(Guid? customerId, string? guestSessionId, CancellationToken cancellationToken)
    {
        var cart = customerId.HasValue
            ? await _context.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
            : await _context.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.GuestSessionId == guestSessionId, cancellationToken);

        if (cart is not null) return cart;

        cart = new Domain.Entities.Cart.Cart { CustomerId = customerId, GuestSessionId = customerId.HasValue ? null : guestSessionId };
        _context.Carts.Add(cart);
        return cart;
    }
}
