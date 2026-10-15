using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Catalog.Commands.UpdateProduct;

public record UpdateProductCommand : IRequest<Result>, IAuditableRequest
{
    public Guid ProductId { get; init; }

    /// <summary>Resolved server-side from ICurrentUserService — the handler verifies this Vendor owns the product.</summary>
    public Guid RequestingVendorId { get; init; }

    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public Guid CategoryId { get; init; }
    public Guid? BrandId { get; init; }
    public decimal BasePrice { get; init; }
    public decimal? SalePrice { get; init; }
    public string? Material { get; init; }

    public string Action => "Product.Updated";
    public string EntityName => "Product";
}

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.BasePrice).GreaterThan(0);
        RuleFor(x => x.SalePrice).GreaterThan(0).LessThan(x => x.BasePrice).When(x => x.SalePrice.HasValue);
    }
}

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdateProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product is null)
            throw new NotFoundException(nameof(Domain.Entities.Catalog.Product), request.ProductId);

        if (product.VendorId != request.RequestingVendorId)
            throw new ForbiddenAccessException("You do not own this product.");

        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
            return Result.Failure("Category does not exist.");

        var listingFieldsChanged = product.Name != request.Name
            || product.Description != request.Description
            || product.CategoryId != request.CategoryId
            || product.BasePrice != request.BasePrice;

        product.Name = request.Name;
        product.Description = request.Description;
        product.CategoryId = request.CategoryId;
        product.BrandId = request.BrandId;
        product.BasePrice = request.BasePrice;
        product.SalePrice = request.SalePrice;
        product.Material = request.Material;

        // Material changes to a live listing send it back through moderation,
        // per the Product Moderation requirement — price-only or stock-only
        // tweaks don't need re-review.
        if (listingFieldsChanged && product.Status == ProductStatus.Active)
            product.Status = ProductStatus.PendingApproval;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
