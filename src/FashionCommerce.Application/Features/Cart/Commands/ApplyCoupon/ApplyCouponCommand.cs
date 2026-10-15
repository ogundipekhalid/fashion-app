using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Cart.Commands.ApplyCoupon;

public record ApplyCouponCommand : IRequest<Result>
{
    public Guid CartId { get; init; }
    public string CouponCode { get; init; } = default!;
}

public class ApplyCouponCommandValidator : AbstractValidator<ApplyCouponCommand>
{
    public ApplyCouponCommandValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
        RuleFor(x => x.CouponCode).NotEmpty().MaximumLength(50);
    }
}

public class ApplyCouponCommandHandler : IRequestHandler<ApplyCouponCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public ApplyCouponCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(ApplyCouponCommand request, CancellationToken cancellationToken)
    {
        var cart = await _context.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == request.CartId, cancellationToken);
        if (cart is null)
            throw new NotFoundException(nameof(Domain.Entities.Cart.Cart), request.CartId);

        var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == request.CouponCode.ToUpperInvariant(), cancellationToken);
        if (coupon is null)
            return Result.Failure("Coupon code not found.");

        if (!coupon.IsCurrentlyValid(DateTime.UtcNow))
            return Result.Failure("This coupon is not currently valid.");

        try
        {
            coupon.CalculateDiscount(cart.Subtotal); // validates MinOrderAmount, throws InvalidCouponException if not met
        }
        catch (InvalidCouponException ex)
        {
            return Result.Failure(ex.Message);
        }

        cart.AppliedCouponCode = coupon.Code;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
