using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Chat;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Chat.Commands.MarkMessagesAsRead;

/// <summary>Marks every unread message in a conversation, not sent by the requester, as read — the read receipt feature from the spec.</summary>
public record MarkMessagesAsReadCommand(Guid ConversationId, Guid RequesterId) : IRequest<Result<int>>;

public class MarkMessagesAsReadCommandValidator : AbstractValidator<MarkMessagesAsReadCommand>
{
    public MarkMessagesAsReadCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
    }
}

public class MarkMessagesAsReadCommandHandler : IRequestHandler<MarkMessagesAsReadCommand, Result<int>>
{
    private readonly IApplicationDbContext _context;

    public MarkMessagesAsReadCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> Handle(MarkMessagesAsReadCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == request.ConversationId, cancellationToken);
        if (conversation is null)
            throw new NotFoundException(nameof(Conversation), request.ConversationId);

        var isParticipant = conversation.CustomerId == request.RequesterId || conversation.VendorId == request.RequesterId;
        if (!isParticipant)
            throw new ForbiddenAccessException("You do not have access to this conversation.");

        var unread = await _context.Messages
            .Where(m => m.ConversationId == request.ConversationId && !m.IsRead && !m.IsBlocked && m.SenderId != request.RequesterId)
            .ToListAsync(cancellationToken);

        foreach (var message in unread)
            message.MarkAsRead();

        await _context.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(unread.Count);
    }
}
