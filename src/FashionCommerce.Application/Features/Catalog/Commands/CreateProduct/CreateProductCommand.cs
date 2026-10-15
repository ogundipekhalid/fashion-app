using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Common.Utilities;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Catalog.Commands.CreateProduct;

public record CreateProductVariantInput
{
    public string Color { get; init; } = default!;
    public string? ColorHexCode { get; init; }
    public string Size { get; init; } = default!;
    public string Sku { get; init; } = default!;
    public decimal? PriceOverride { get; init; }
    public int StockQuantity { get; init; }
}

/// <summary>Image URL is expected to already be uploaded to storage (Cloudinary/Azure Blob) by the client before calling this — see Storage module (not yet built) for the upload endpoint.</summary>
public record CreateProductImageInput
{
    public string Url { get; init; } = default!;
    public string? AltText { get; init; }
    public string Angle { get; init; } = "Front";
    public bool IsPrimary { get; init; }
}

public record CreateProductCommand : IRequest<Result<Guid>>, IAuditableRequest
{
    /// <summary>Resolved server-side from ICurrentUserService in the handler, not trusted from the request body.</summary>
    public Guid VendorId { get; init; }

    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public Guid CategoryId { get; init; }
    public Guid? BrandId { get; init; }
    public string? TargetGender { get; init; }
    public string Season { get; init; } = "AllSeason";
    public string? Material { get; init; }
    public decimal BasePrice { get; init; }
    public decimal? SalePrice { get; init; }
    public string Sku { get; init; } = default!;
    public string? Barcode { get; init; }
    public List<string> Tags { get; init; } = new();
    public List<CreateProductVariantInput> Variants { get; init; } = new();
    public List<CreateProductImageInput> Images { get; init; } = new();

    public string Action => "Product.Created";
    public string EntityName => "Product";
}

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.BasePrice).GreaterThan(0);
        RuleFor(x => x.SalePrice).GreaterThan(0).LessThan(x => x.BasePrice)
            .When(x => x.SalePrice.HasValue)
            .WithMessage("Sale price must be greater than 0 and less than the base price.");
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(100);

        RuleFor(x => x.Season).Must(s => Enum.TryParse<Season>(s, out _)).WithMessage("Invalid season.");
        RuleFor(x => x.TargetGender)
            .Must(g => Enum.TryParse<Gender>(g, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.TargetGender))
            .WithMessage("Invalid gender.");

        RuleFor(x => x.Variants).NotEmpty().WithMessage("At least one variant (color/size combination) is required.");

        RuleForEach(x => x.Variants).ChildRules(variant =>
        {
            variant.RuleFor(v => v.Color).NotEmpty().MaximumLength(50);
            variant.RuleFor(v => v.Size).NotEmpty().MaximumLength(20);
            variant.RuleFor(v => v.Sku).NotEmpty().MaximumLength(100);
            variant.RuleFor(v => v.StockQuantity).GreaterThanOrEqualTo(0);
        });

        RuleForEach(x => x.Images).ChildRules(image =>
        {
            image.RuleFor(i => i.Url).NotEmpty().MaximumLength(2048);
            image.RuleFor(i => i.Angle).Must(a => Enum.TryParse<ProductImageAngle>(a, out _)).WithMessage("Invalid image angle.");
        });
    }
}

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == request.VendorId, cancellationToken);
        if (vendor is null)
            return Result<Guid>.Failure("Vendor not found.");

        if (vendor.VerificationStatus != VendorVerificationStatus.Verified)
            return Result<Guid>.Failure("Only verified vendors can list products.");

        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
            return Result<Guid>.Failure("Category does not exist.");

        if (await _context.Products.AnyAsync(p => p.Sku == request.Sku, cancellationToken))
            return Result<Guid>.Failure("A product with this SKU already exists.");

        var variantSkus = request.Variants.Select(v => v.Sku).ToList();
        if (await _context.ProductVariants.AnyAsync(v => variantSkus.Contains(v.Sku), cancellationToken))
            return Result<Guid>.Failure("One or more variant SKUs are already in use.");

        var slug = SlugGenerator.Generate($"{request.Name}-{request.Sku}"); // SKU suffix keeps slugs unique even for similarly-named products across vendors

        var product = new Product
        {
            VendorId = request.VendorId,
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            Name = request.Name,
            Slug = slug,
            Description = request.Description,
            TargetGender = string.IsNullOrWhiteSpace(request.TargetGender) ? null : Enum.Parse<Gender>(request.TargetGender),
            Season = Enum.Parse<Season>(request.Season),
            Material = request.Material,
            BasePrice = request.BasePrice,
            SalePrice = request.SalePrice,
            Sku = request.Sku,
            Barcode = request.Barcode,
            Status = ProductStatus.PendingApproval
        };

        foreach (var variantInput in request.Variants)
        {
            product.Variants.Add(new ProductVariant
            {
                ProductId = product.Id,
                Color = variantInput.Color,
                ColorHexCode = variantInput.ColorHexCode,
                Size = variantInput.Size,
                Sku = variantInput.Sku,
                PriceOverride = variantInput.PriceOverride,
                StockQuantity = variantInput.StockQuantity
            });
        }

        foreach (var imageInput in request.Images)
        {
            product.Images.Add(new ProductImage
            {
                ProductId = product.Id,
                Url = imageInput.Url,
                AltText = imageInput.AltText,
                Angle = Enum.Parse<ProductImageAngle>(imageInput.Angle),
                IsPrimary = imageInput.IsPrimary
            });
        }

        foreach (var tag in request.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct())
        {
            product.Tags.Add(new ProductTag { ProductId = product.Id, Tag = tag.Trim() });
        }

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(product.Id);
    }
}
