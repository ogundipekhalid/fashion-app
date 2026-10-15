using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Cart.Commands.RemoveCoupon;

public record RemoveCouponCommand : IRequest<Result>
{
    public Guid CartId { get; init; }
}

public class RemoveCouponCommandValidator : AbstractValidator<RemoveCouponCommand>
{
    public RemoveCouponCommandValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
    }
}

public class RemoveCouponCommandHandler : IRequestHandler<RemoveCouponCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public RemoveCouponCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(RemoveCouponCommand request, CancellationToken cancellationToken)
    {
        var cart = await _context.Carts.FirstOrDefaultAsync(c => c.Id == request.CartId, cancellationToken);
        if (cart is null)
            throw new NotFoundException(nameof(Domain.Entities.Cart.Cart), request.CartId);

        cart.AppliedCouponCode = null;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
