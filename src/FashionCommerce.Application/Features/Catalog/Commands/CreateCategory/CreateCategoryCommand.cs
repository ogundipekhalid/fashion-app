using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Common.Utilities;
using FashionCommerce.Domain.Entities.Catalog;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Catalog.Commands.CreateCategory;

public record CreateCategoryCommand : IRequest<Result<Guid>>, IAuditableRequest
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public Guid? ParentCategoryId { get; init; }

    public string Action => "Category.Created";
    public string EntityName => "Category";
}

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (request.ParentCategoryId.HasValue)
        {
            var parentExists = await _context.Categories.AnyAsync(c => c.Id == request.ParentCategoryId, cancellationToken);
            if (!parentExists)
                return Result<Guid>.Failure("Parent category does not exist.");
        }

        var slug = SlugGenerator.Generate(request.Name);
        if (await _context.Categories.AnyAsync(c => c.Slug == slug, cancellationToken))
            return Result<Guid>.Failure("A category with this name already exists.");

        var category = new Category
        {
            Name = request.Name,
            Slug = slug,
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            ParentCategoryId = request.ParentCategoryId
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(category.Id);
    }
}
