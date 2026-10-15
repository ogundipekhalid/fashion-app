using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Common.Utilities;
using FashionCommerce.Domain.Entities.Catalog;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Catalog.Commands.CreateBrand;

public record CreateBrandCommand : IRequest<Result<Guid>>, IAuditableRequest
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string? LogoUrl { get; init; }

    public string Action => "Brand.Created";
    public string EntityName => "Brand";
}

public class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public class CreateBrandCommandHandler : IRequestHandler<CreateBrandCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateBrandCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
    {
        var slug = SlugGenerator.Generate(request.Name);
        if (await _context.Brands.AnyAsync(b => b.Slug == slug, cancellationToken))
            return Result<Guid>.Failure("A brand with this name already exists.");

        var brand = new Brand
        {
            Name = request.Name,
            Slug = slug,
            Description = request.Description,
            LogoUrl = request.LogoUrl
        };

        _context.Brands.Add(brand);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(brand.Id);
    }
}
