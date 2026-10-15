using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Styling;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Styling.Commands.LikeStylePost;

public record LikeStylePostCommand : IRequest<Result>
{
    public Guid StylePostId { get; init; }
    public Guid UserId { get; init; }
    public string UserRole { get; init; } = default!;
}

public class LikeStylePostCommandValidator : AbstractValidator<LikeStylePostCommand>
{
    public LikeStylePostCommandValidator()
    {
        RuleFor(x => x.StylePostId).NotEmpty();
        RuleFor(x => x.UserRole).Must(r => Enum.TryParse<UserRole>(r, out _)).WithMessage("Invalid user role.");
    }
}

public class LikeStylePostCommandHandler : IRequestHandler<LikeStylePostCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public LikeStylePostCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(LikeStylePostCommand request, CancellationToken cancellationToken)
    {
        var post = await _context.StylePosts.FirstOrDefaultAsync(p => p.Id == request.StylePostId, cancellationToken);
        if (post is null)
            throw new NotFoundException(nameof(StylePost), request.StylePostId);

        var alreadyLiked = await _context.StylePostLikes
            .AnyAsync(l => l.StylePostId == request.StylePostId && l.UserId == request.UserId, cancellationToken);

        if (alreadyLiked)
            return Result.Success(); // idempotent — liking twice is a no-op, not an error

        _context.StylePostLikes.Add(new StylePostLike
        {
            StylePostId = request.StylePostId,
            UserId = request.UserId,
            UserRole = Enum.Parse<UserRole>(request.UserRole)
        });

        post.IncrementLikeCount();
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
