using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Catalog.DTOs;
using FashionCommerce.Domain.Entities.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Catalog.Queries.GetProductById;

public record GetProductByIdQuery(Guid ProductId) : IRequest<ProductDetailDto>;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetProductByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProductDetailDto> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Vendor)
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
            throw new NotFoundException(nameof(Product), request.ProductId);

        return new ProductDetailDto(
            product.Id, product.Name, product.Slug, product.Description,
            product.VendorId, product.Vendor.BusinessName,
            product.CategoryId, product.Category.Name,
            product.Brand?.Name,
            product.BasePrice, product.SalePrice, product.CurrentPrice,
            product.Material, product.Season.ToString(), product.TargetGender?.ToString(),
            product.AverageRating, product.ReviewCount, product.TotalSold,
            product.Status.ToString(),
            product.Variants.Select(v => new ProductVariantDto(
                v.Id, v.Color, v.ColorHexCode, v.Size, v.Sku,
                v.PriceOverride ?? product.CurrentPrice, v.StockQuantity, v.IsInStock, v.IsLowStock)).ToList(),
            product.Images.OrderBy(i => i.DisplayOrder).Select(i => new ProductImageDto(
                i.Id, i.Url, i.AltText, i.Angle.ToString(), i.IsPrimary, i.DisplayOrder)).ToList(),
            product.Tags.Select(t => t.Tag).ToList());
    }
}
