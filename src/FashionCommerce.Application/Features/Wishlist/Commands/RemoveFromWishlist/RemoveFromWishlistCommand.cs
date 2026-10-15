using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Wishlist.Commands.RemoveFromWishlist;

public record RemoveFromWishlistCommand : IRequest<Result>
{
    public Guid WishlistItemId { get; init; }
    public Guid CustomerId { get; init; }
}

public class RemoveFromWishlistCommandValidator : AbstractValidator<RemoveFromWishlistCommand>
{
    public RemoveFromWishlistCommandValidator()
    {
        RuleFor(x => x.WishlistItemId).NotEmpty();
    }
}

public class RemoveFromWishlistCommandHandler : IRequestHandler<RemoveFromWishlistCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public RemoveFromWishlistCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(RemoveFromWishlistCommand request, CancellationToken cancellationToken)
    {
        var item = await _context.WishlistItems.FirstOrDefaultAsync(
            w => w.Id == request.WishlistItemId && w.CustomerId == request.CustomerId, cancellationToken);

        if (item is null)
            return Result.Success(); // idempotent — already gone

        _context.WishlistItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
