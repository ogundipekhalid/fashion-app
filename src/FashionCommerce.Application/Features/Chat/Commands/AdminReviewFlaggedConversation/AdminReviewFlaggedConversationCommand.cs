using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Chat;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Chat.Commands.AdminReviewFlaggedConversation;

public enum FlaggedConversationDecision { ReopenConversation, KeepBlockedAndCloseCase }

public record AdminReviewFlaggedConversationCommand : IRequest<Result>, IAuditableRequest
{
    public Guid ConversationId { get; init; }
    public FlaggedConversationDecision Decision { get; init; }
    public string? AdminNote { get; init; }

    public string Action => "Conversation.ReviewedByAdmin";
    public string EntityName => "Conversation";
}

public class AdminReviewFlaggedConversationCommandValidator : AbstractValidator<AdminReviewFlaggedConversationCommand>
{
    public AdminReviewFlaggedConversationCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
    }
}

public class AdminReviewFlaggedConversationCommandHandler : IRequestHandler<AdminReviewFlaggedConversationCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public AdminReviewFlaggedConversationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(AdminReviewFlaggedConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == request.ConversationId, cancellationToken);
        if (conversation is null)
            throw new NotFoundException(nameof(Conversation), request.ConversationId);

        if (request.Decision == FlaggedConversationDecision.ReopenConversation)
            conversation.ClearFlagAndReopen();
        else
            conversation.Close();

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
