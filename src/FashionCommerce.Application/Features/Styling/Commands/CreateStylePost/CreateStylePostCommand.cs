using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Styling;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Styling.Commands.CreateStylePost;

public record CreateStylePostCommand : IRequest<Result<Guid>>, IAuditableRequest
{
    /// <summary>Resolved server-side — the Customer.Id or Vendor.Id of the poster.</summary>
    public Guid AuthorId { get; init; }
    public string AuthorType { get; init; } = default!; // "Customer" | "Vendor"

    public Guid ProductId { get; init; }
    public string? Caption { get; init; }
    public List<string> ImageUrls { get; init; } = new();
    public List<Guid> TaggedProductIds { get; init; } = new();

    public string Action => "StylePost.Created";
    public string EntityName => "StylePost";
}

public class CreateStylePostCommandValidator : AbstractValidator<CreateStylePostCommand>
{
    public CreateStylePostCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.AuthorType).Must(t => Enum.TryParse<StylePostAuthorType>(t, out _)).WithMessage("Invalid author type.");
        RuleFor(x => x.ImageUrls).NotEmpty().WithMessage("At least one photo is required.");
        RuleForEach(x => x.ImageUrls).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.Caption).MaximumLength(500);
        RuleFor(x => x.TaggedProductIds).Must(t => t.Count <= 10).WithMessage("You can tag up to 10 additional products.");
    }
}

public class CreateStylePostCommandHandler : IRequestHandler<CreateStylePostCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateStylePostCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateStylePostCommand request, CancellationToken cancellationToken)
    {
        var productExists = await _context.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken);
        if (!productExists)
            throw new NotFoundException(nameof(Domain.Entities.Catalog.Product), request.ProductId);

        if (request.TaggedProductIds.Count > 0)
        {
            var foundCount = await _context.Products.CountAsync(p => request.TaggedProductIds.Contains(p.Id), cancellationToken);
            if (foundCount != request.TaggedProductIds.Distinct().Count())
                return Result<Guid>.Failure("One or more tagged products do not exist.");
        }

        var post = StylePost.Create(request.AuthorId, Enum.Parse<StylePostAuthorType>(request.AuthorType), request.ProductId, request.Caption, request.ImageUrls);

        foreach (var taggedProductId in request.TaggedProductIds.Distinct().Where(id => id != request.ProductId))
            post.TaggedProducts.Add(new StylePostProductTag { StylePostId = post.Id, ProductId = taggedProductId });

        _context.StylePosts.Add(post);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(post.Id);
    }
}
