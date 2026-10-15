using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Styling;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Styling.Commands.UnlikeStylePost;

public record UnlikeStylePostCommand : IRequest<Result>
{
    public Guid StylePostId { get; init; }
    public Guid UserId { get; init; }
}

public class UnlikeStylePostCommandValidator : AbstractValidator<UnlikeStylePostCommand>
{
    public UnlikeStylePostCommandValidator()
    {
        RuleFor(x => x.StylePostId).NotEmpty();
    }
}

public class UnlikeStylePostCommandHandler : IRequestHandler<UnlikeStylePostCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UnlikeStylePostCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UnlikeStylePostCommand request, CancellationToken cancellationToken)
    {
        var post = await _context.StylePosts.FirstOrDefaultAsync(p => p.Id == request.StylePostId, cancellationToken);
        if (post is null)
            throw new NotFoundException(nameof(StylePost), request.StylePostId);

        var like = await _context.StylePostLikes
            .FirstOrDefaultAsync(l => l.StylePostId == request.StylePostId && l.UserId == request.UserId, cancellationToken);

        if (like is null)
            return Result.Success(); // idempotent

        _context.StylePostLikes.Remove(like);
        post.DecrementLikeCount();
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
