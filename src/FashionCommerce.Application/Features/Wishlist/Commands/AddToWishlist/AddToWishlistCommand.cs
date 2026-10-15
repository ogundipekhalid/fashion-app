using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Wishlist;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Wishlist.Commands.AddToWishlist;

public record AddToWishlistCommand : IRequest<Result<Guid>>
{
    public Guid CustomerId { get; init; }
    public Guid ProductId { get; init; }
    public Guid? ProductVariantId { get; init; }
}

public class AddToWishlistCommandValidator : AbstractValidator<AddToWishlistCommand>
{
    public AddToWishlistCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

public class AddToWishlistCommandHandler : IRequestHandler<AddToWishlistCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public AddToWishlistCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(AddToWishlistCommand request, CancellationToken cancellationToken)
    {
        var productExists = await _context.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken);
        if (!productExists)
            throw new NotFoundException(nameof(Domain.Entities.Catalog.Product), request.ProductId);

        var existing = await _context.WishlistItems.FirstOrDefaultAsync(w =>
            w.CustomerId == request.CustomerId && w.ProductId == request.ProductId && w.ProductVariantId == request.ProductVariantId,
            cancellationToken);

        if (existing is not null)
            return Result<Guid>.Success(existing.Id); // idempotent

        var item = new WishlistItem
        {
            CustomerId = request.CustomerId,
            ProductId = request.ProductId,
            ProductVariantId = request.ProductVariantId
        };

        _context.WishlistItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(item.Id);
    }
}
