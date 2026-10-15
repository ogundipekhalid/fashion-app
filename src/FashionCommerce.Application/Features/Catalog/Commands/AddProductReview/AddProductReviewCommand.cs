using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Catalog.Commands.AddProductReview;

public record AddProductReviewCommand : IRequest<Result<Guid>>, IAuditableRequest
{
    public Guid ProductId { get; init; }
    public Guid CustomerId { get; init; }
    public int Rating { get; init; }
    public string? Title { get; init; }
    public string? Comment { get; init; }

    /// <summary>If provided, must be a Delivered OrderItem belonging to this customer for this product — enables "verified purchase".</summary>
    public Guid? OrderItemId { get; init; }

    public string Action => "ProductReview.Added";
    public string EntityName => "ProductReview";
}

public class AddProductReviewCommandValidator : AbstractValidator<AddProductReviewCommand>
{
    public AddProductReviewCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Comment).MaximumLength(2000);
    }
}

public class AddProductReviewCommandHandler : IRequestHandler<AddProductReviewCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public AddProductReviewCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(AddProductReviewCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
            throw new NotFoundException(nameof(Product), request.ProductId);

        if (product.Reviews.Any(r => r.CustomerId == request.CustomerId && !r.IsDeleted))
            return Result<Guid>.Failure("You have already reviewed this product.");

        var verifiedOrderItemId = request.OrderItemId;
        if (verifiedOrderItemId.HasValue)
        {
            var isValidPurchase = await _context.OrderItems
                .Where(oi => oi.Id == verifiedOrderItemId
                    && oi.ProductVariant.ProductId == request.ProductId
                    && oi.Status == OrderItemStatus.Delivered
                    && oi.Order.CustomerId == request.CustomerId)
                .AnyAsync(cancellationToken);

            if (!isValidPurchase) verifiedOrderItemId = null; // silently downgrade to unverified rather than reject the review outright
        }

        var review = ProductReview.Create(request.ProductId, request.CustomerId, request.Rating, request.Title, request.Comment, verifiedOrderItemId);

        _context.ProductReviews.Add(review);
        await _context.SaveChangesAsync(cancellationToken);

        // Recalculate the denormalized aggregate now that the new review is persisted.
        product.RecalculateRating(product.Reviews.Append(review));
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(review.Id);
    }
}
