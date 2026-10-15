using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Cart.Commands.MergeGuestCart;

/// <summary>
/// Called right after a Customer logs in if the client was holding a guest
/// cart. Quantities are summed for variants present in both carts (capped
/// at available stock); the guest cart is then deleted.
/// </summary>
public record MergeGuestCartCommand : IRequest<Result<Guid>>
{
    public Guid CustomerId { get; init; }
    public string GuestSessionId { get; init; } = default!;
}

public class MergeGuestCartCommandValidator : AbstractValidator<MergeGuestCartCommand>
{
    public MergeGuestCartCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.GuestSessionId).NotEmpty();
    }
}

public class MergeGuestCartCommandHandler : IRequestHandler<MergeGuestCartCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public MergeGuestCartCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(MergeGuestCartCommand request, CancellationToken cancellationToken)
    {
        var guestCart = await _context.Carts
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant)
            .FirstOrDefaultAsync(c => c.GuestSessionId == request.GuestSessionId, cancellationToken);

        var customerCart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == request.CustomerId, cancellationToken);

        if (guestCart is null)
            return Result<Guid>.Success(customerCart?.Id ?? Guid.Empty); // nothing to merge

        if (customerCart is null)
        {
            // No existing customer cart — the guest cart simply becomes theirs.
            guestCart.CustomerId = request.CustomerId;
            guestCart.GuestSessionId = null;
            await _context.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(guestCart.Id);
        }

        foreach (var guestItem in guestCart.Items)
        {
            var existing = customerCart.Items.FirstOrDefault(i => i.ProductVariantId == guestItem.ProductVariantId);
            if (existing is not null)
            {
                var combined = existing.Quantity + guestItem.Quantity;
                existing.Quantity = Math.Min(combined, guestItem.ProductVariant.StockQuantity == 0 ? combined : guestItem.ProductVariant.StockQuantity);
            }
            else
            {
                customerCart.Items.Add(new Domain.Entities.Cart.CartItem
                {
                    CartId = customerCart.Id,
                    ProductVariantId = guestItem.ProductVariantId,
                    Quantity = guestItem.Quantity,
                    PriceAtAdd = guestItem.PriceAtAdd
                });
            }
        }

        _context.Carts.Remove(guestCart);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(customerCart.Id);
    }
}
