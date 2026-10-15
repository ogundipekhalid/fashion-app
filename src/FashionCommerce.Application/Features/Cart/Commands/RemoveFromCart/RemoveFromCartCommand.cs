using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Cart.Commands.RemoveFromCart;

public record RemoveFromCartCommand : IRequest<Result>
{
    public Guid CartItemId { get; init; }
    public Guid? RequestingCustomerId { get; init; }
    public string? RequestingGuestSessionId { get; init; }
}

public class RemoveFromCartCommandValidator : AbstractValidator<RemoveFromCartCommand>
{
    public RemoveFromCartCommandValidator()
    {
        RuleFor(x => x.CartItemId).NotEmpty();
    }
}

public class RemoveFromCartCommandHandler : IRequestHandler<RemoveFromCartCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public RemoveFromCartCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(RemoveFromCartCommand request, CancellationToken cancellationToken)
    {
        var item = await _context.CartItems
            .Include(i => i.Cart)
            .FirstOrDefaultAsync(i => i.Id == request.CartItemId, cancellationToken);

        if (item is null)
            throw new NotFoundException("CartItem", request.CartItemId);

        var owns = request.RequestingCustomerId.HasValue
            ? item.Cart.CustomerId == request.RequestingCustomerId
            : item.Cart.GuestSessionId == request.RequestingGuestSessionId;

        if (!owns)
            throw new ForbiddenAccessException("This cart item does not belong to you.");

        _context.CartItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
