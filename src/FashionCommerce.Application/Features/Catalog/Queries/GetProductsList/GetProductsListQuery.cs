using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Catalog.DTOs;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Catalog.Queries.GetProductsList;

public enum ProductSortBy { Newest, PriceLowToHigh, PriceHighToLow, TopRated, BestSelling }

public record GetProductsListQuery : IRequest<PaginatedList<ProductSummaryDto>>
{
    public string? SearchTerm { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? BrandId { get; init; }
    public Guid? VendorId { get; init; }
    public string? Gender { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public ProductSortBy SortBy { get; init; } = ProductSortBy.Newest;

    /// <summary>Only Admin/Vendor-scoped requests should be allowed to see non-Active products — enforced by the handler defaulting to Active-only unless explicitly overridden by an authorized caller (checked at the Controller/Application boundary).</summary>
    public bool IncludeNonActive { get; init; }

    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetProductsListQueryHandler : IRequestHandler<GetProductsListQuery, PaginatedList<ProductSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductsListQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ProductSummaryDto>> Handle(GetProductsListQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Products.AsQueryable();

        query = request.IncludeNonActive
            ? query
            : query.Where(p => p.Status == ProductStatus.Active);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            query = query.Where(p => p.Name.Contains(request.SearchTerm) || p.Description.Contains(request.SearchTerm));

        if (request.CategoryId.HasValue) query = query.Where(p => p.CategoryId == request.CategoryId);
        if (request.BrandId.HasValue) query = query.Where(p => p.BrandId == request.BrandId);
        if (request.VendorId.HasValue) query = query.Where(p => p.VendorId == request.VendorId);

        if (!string.IsNullOrWhiteSpace(request.Gender) && Enum.TryParse<Gender>(request.Gender, out var gender))
            query = query.Where(p => p.TargetGender == gender);

        if (request.MinPrice.HasValue) query = query.Where(p => p.BasePrice >= request.MinPrice);
        if (request.MaxPrice.HasValue) query = query.Where(p => p.BasePrice <= request.MaxPrice);

        query = request.SortBy switch
        {
            ProductSortBy.PriceLowToHigh => query.OrderBy(p => p.BasePrice),
            ProductSortBy.PriceHighToLow => query.OrderByDescending(p => p.BasePrice),
            ProductSortBy.TopRated => query.OrderByDescending(p => p.AverageRating),
            ProductSortBy.BestSelling => query.OrderByDescending(p => p.TotalSold),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        var projected = query.Select(p => new ProductSummaryDto(
            p.Id, p.Name, p.Slug, p.CurrentPrice, p.BasePrice,
            p.AverageRating, p.ReviewCount,
            p.Images.Where(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault(),
            p.Vendor.BusinessName, p.Status.ToString()));

        return await PaginatedList<ProductSummaryDto>.CreateAsync(projected, request.PageNumber, request.PageSize);
    }
}
