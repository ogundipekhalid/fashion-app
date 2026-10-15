using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace FashionCommerce.Application.Features.AI.Commands.GenerateProductContent;

public record ProductContentSuggestionDto(string Description, string SeoTitle, string SeoDescription, List<string> Tags);

/// <summary>
/// A draft, not a publish action — the vendor reviews and edits the
/// suggestion before it ever goes into CreateProductCommand/UpdateProductCommand.
/// Nothing here writes to the database.
/// </summary>
public record GenerateProductContentCommand : IRequest<Result<ProductContentSuggestionDto>>
{
    public string ProductName { get; init; } = default!;
    public string Category { get; init; } = default!;
    public string? Material { get; init; }
    public string? TargetGender { get; init; }
}

public class GenerateProductContentCommandValidator : AbstractValidator<GenerateProductContentCommand>
{
    public GenerateProductContentCommandValidator()
    {
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Category).NotEmpty();
    }
}

public class GenerateProductContentCommandHandler : IRequestHandler<GenerateProductContentCommand, Result<ProductContentSuggestionDto>>
{
    private readonly IAiService _aiService;

    public GenerateProductContentCommandHandler(IAiService aiService)
    {
        _aiService = aiService;
    }

    public async Task<Result<ProductContentSuggestionDto>> Handle(GenerateProductContentCommand request, CancellationToken cancellationToken)
    {
        var suggestion = await _aiService.GenerateProductContentAsync(
            request.ProductName, request.Category, request.Material, request.TargetGender, cancellationToken);

        if (!suggestion.Succeeded)
            return Result<ProductContentSuggestionDto>.Failure(suggestion.ErrorMessage ?? "AI content generation failed.");

        return Result<ProductContentSuggestionDto>.Success(new ProductContentSuggestionDto(
            suggestion.Description!, suggestion.SeoTitle!, suggestion.SeoDescription!, suggestion.Tags!));
    }
}
