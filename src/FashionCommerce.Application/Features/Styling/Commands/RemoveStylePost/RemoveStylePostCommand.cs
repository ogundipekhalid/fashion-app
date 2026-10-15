using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Styling;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Styling.Commands.RemoveStylePost;

public record RemoveStylePostCommand : IRequest<Result>, IAuditableRequest
{
    public Guid StylePostId { get; init; }

    /// <summary>Null when an Admin is removing it for moderation reasons; set when the author is deleting their own post.</summary>
    public Guid? RequestingAuthorId { get; init; }
    public string? Reason { get; init; }

    public string Action => "StylePost.Removed";
    public string EntityName => "StylePost";
}

public class RemoveStylePostCommandValidator : AbstractValidator<RemoveStylePostCommand>
{
    public RemoveStylePostCommandValidator()
    {
        RuleFor(x => x.StylePostId).NotEmpty();
    }
}

public class RemoveStylePostCommandHandler : IRequestHandler<RemoveStylePostCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public RemoveStylePostCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(RemoveStylePostCommand request, CancellationToken cancellationToken)
    {
        var post = await _context.StylePosts.FirstOrDefaultAsync(p => p.Id == request.StylePostId, cancellationToken);
        if (post is null)
            throw new NotFoundException(nameof(StylePost), request.StylePostId);

        if (request.RequestingAuthorId.HasValue && post.AuthorId != request.RequestingAuthorId.Value)
            throw new ForbiddenAccessException("You do not own this post.");

        post.RemoveByModeration(request.Reason ?? "Removed by author.");
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
