using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Catalog.DTOs;
using FashionCommerce.Domain.Entities.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Catalog.Queries.GetCategories;

public record GetCategoriesQuery : IRequest<List<CategoryDto>>;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var all = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);

        return BuildTree(all, null);
    }

    private static List<CategoryDto> BuildTree(List<Category> all, Guid? parentId) =>
        all.Where(c => c.ParentCategoryId == parentId)
           .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.ImageUrl, c.ParentCategoryId, BuildTree(all, c.Id)))
           .ToList();
}
