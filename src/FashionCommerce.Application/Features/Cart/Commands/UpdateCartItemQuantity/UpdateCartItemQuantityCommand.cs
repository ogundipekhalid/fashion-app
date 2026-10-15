using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Cart.Commands.UpdateCartItemQuantity;

public record UpdateCartItemQuantityCommand : IRequest<Result>
{
    public Guid CartItemId { get; init; }
    public int Quantity { get; init; }

    /// <summary>Ownership check: must match the CartItem's parent Cart's CustomerId or GuestSessionId.</summary>
    public Guid? RequestingCustomerId { get; init; }
    public string? RequestingGuestSessionId { get; init; }
}

public class UpdateCartItemQuantityCommandValidator : AbstractValidator<UpdateCartItemQuantityCommand>
{
    public UpdateCartItemQuantityCommandValidator()
    {
        RuleFor(x => x.CartItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(50);
    }
}

public class UpdateCartItemQuantityCommandHandler : IRequestHandler<UpdateCartItemQuantityCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdateCartItemQuantityCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdateCartItemQuantityCommand request, CancellationToken cancellationToken)
    {
        var item = await _context.CartItems
            .Include(i => i.Cart)
            .Include(i => i.ProductVariant)
            .FirstOrDefaultAsync(i => i.Id == request.CartItemId, cancellationToken);

        if (item is null)
            throw new NotFoundException("CartItem", request.CartItemId);

        var owns = request.RequestingCustomerId.HasValue
            ? item.Cart.CustomerId == request.RequestingCustomerId
            : item.Cart.GuestSessionId == request.RequestingGuestSessionId;

        if (!owns)
            throw new ForbiddenAccessException("This cart item does not belong to you.");

        if (item.ProductVariant.StockQuantity < request.Quantity)
            return Result.Failure($"Only {item.ProductVariant.StockQuantity} unit(s) left in stock.");

        item.Quantity = request.Quantity;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
