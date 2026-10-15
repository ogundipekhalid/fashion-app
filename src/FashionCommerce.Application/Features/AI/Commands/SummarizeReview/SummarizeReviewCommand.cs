using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Catalog;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.AI.Commands.SummarizeReview;

public record SummarizeReviewCommand(Guid ReviewId) : IRequest<Result<string>>;

public class SummarizeReviewCommandValidator : AbstractValidator<SummarizeReviewCommand>
{
    public SummarizeReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId).NotEmpty();
    }
}

public class SummarizeReviewCommandHandler : IRequestHandler<SummarizeReviewCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAiService _aiService;

    public SummarizeReviewCommandHandler(IApplicationDbContext context, IAiService aiService)
    {
        _context = context;
        _aiService = aiService;
    }

    public async Task<Result<string>> Handle(SummarizeReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _context.ProductReviews.FirstOrDefaultAsync(r => r.Id == request.ReviewId, cancellationToken);
        if (review is null)
            throw new NotFoundException(nameof(ProductReview), request.ReviewId);

        if (string.IsNullOrWhiteSpace(review.Comment))
            return Result<string>.Failure("This review has no comment text to summarize.");

        var result = await _aiService.SummarizeReviewAsync(review.Comment, cancellationToken);
        if (!result.Succeeded)
            return Result<string>.Failure(result.ErrorMessage ?? "Summarization failed.");

        review.AiSummary = result.Text;
        await _context.SaveChangesAsync(cancellationToken);

        return Result<string>.Success(result.Text!);
    }
}
