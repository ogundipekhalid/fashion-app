using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Styling;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Styling.Commands.AddStylePostComment;

public record AddStylePostCommentCommand : IRequest<Result<Guid>>, IAuditableRequest
{
    public Guid StylePostId { get; init; }
    public Guid UserId { get; init; }
    public string UserRole { get; init; } = default!;
    public string Content { get; init; } = default!;

    public string Action => "StylePostComment.Added";
    public string EntityName => "StylePostComment";
}

public class AddStylePostCommentCommandValidator : AbstractValidator<AddStylePostCommentCommand>
{
    public AddStylePostCommentCommandValidator()
    {
        RuleFor(x => x.StylePostId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.UserRole).Must(r => Enum.TryParse<UserRole>(r, out _)).WithMessage("Invalid user role.");
    }
}

public class AddStylePostCommentCommandHandler : IRequestHandler<AddStylePostCommentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public AddStylePostCommentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(AddStylePostCommentCommand request, CancellationToken cancellationToken)
    {
        var post = await _context.StylePosts.FirstOrDefaultAsync(p => p.Id == request.StylePostId, cancellationToken);
        if (post is null)
            throw new NotFoundException(nameof(StylePost), request.StylePostId);

        var comment = StylePostComment.Create(request.StylePostId, request.UserId, Enum.Parse<UserRole>(request.UserRole), request.Content);

        _context.StylePostComments.Add(comment);
        post.IncrementCommentCount();

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(comment.Id);
    }
}
