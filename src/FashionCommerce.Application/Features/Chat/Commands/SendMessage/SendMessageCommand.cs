using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Chat;
using FashionCommerce.Domain.Entities.Notifications;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Chat.Commands.SendMessage;

public record SendMessageCommand : IRequest<Result<Guid>>, IAuditableRequest
{
    public Guid ConversationId { get; init; }

    /// <summary>Resolved server-side — the sender's Customer.Id or Vendor.Id.</summary>
    public Guid SenderId { get; init; }
    public string SenderType { get; init; } = default!; // "Customer" | "Vendor"

    public string? Content { get; init; }
    public string? AttachmentUrl { get; init; }
    public string Type { get; init; } = "Text";

    public string Action => "Message.Sent";
    public string EntityName => "Message";
}

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.SenderType).Must(t => Enum.TryParse<MessageSenderType>(t, out _)).WithMessage("Invalid sender type.");
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Content) || !string.IsNullOrWhiteSpace(x.AttachmentUrl))
            .WithMessage("Message must have content or an attachment.");
    }
}

public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, Result<Guid>>
{
    private const int RecentMessageHistoryWindow = 5;

    private readonly IApplicationDbContext _context;
    private readonly IChatModerationService _moderation;

    public SendMessageCommandHandler(IApplicationDbContext context, IChatModerationService moderation)
    {
        _context = context;
        _moderation = moderation;
    }

    public async Task<Result<Guid>> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, cancellationToken);

        if (conversation is null)
            throw new NotFoundException(nameof(Conversation), request.ConversationId);

        if (!conversation.CanAcceptMessages)
            return Result<Guid>.Failure(conversation.Status == ConversationStatus.Blocked
                ? "This conversation has been blocked pending review and cannot receive new messages."
                : "This conversation is closed.");

        var senderType = Enum.Parse<MessageSenderType>(request.SenderType);

        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            var recentHistory = await _context.Messages
                .Where(m => m.ConversationId == request.ConversationId && m.SenderId == request.SenderId)
                .OrderByDescending(m => m.SentAt)
                .Take(RecentMessageHistoryWindow)
                .Select(m => m.Content ?? string.Empty)
                .ToListAsync(cancellationToken);

            recentHistory.Reverse(); // oldest-first, so the "last N including this one" window reads chronologically

            var moderationResult = _moderation.CheckMessage(request.Content, recentHistory);

            if (moderationResult.IsSuspicious)
            {
                // The offending message is still stored, but marked Blocked — it's
                // never surfaced to the other participant, only visible to Admin
                // reviewing the flagged conversation's audit trail.
                var blockedMessage = new Message
                {
                    ConversationId = conversation.Id,
                    SenderId = request.SenderId,
                    SenderType = senderType,
                    Type = MessageType.Text,
                    Content = request.Content,
                    SentAt = DateTime.UtcNow
                };
                blockedMessage.Block(moderationResult.Reason!);
                _context.Messages.Add(blockedMessage);

                conversation.FlagAndBlock(moderationResult.Reason!);

                // Notify Admin — a lightweight in-app Notification for now; a full
                // SupportTicket escalation can be layered on in the Support module.
                _context.Notifications.Add(new Notification
                {
                    UserId = Guid.Empty, // broadcast-style admin notification; targeted admin routing can be added once admin assignment rules exist
                    UserRole = UserRole.Admin,
                    Type = NotificationType.SupportTicketUpdated,
                    Title = "Chat flagged for suspicious content",
                    Body = $"Conversation {conversation.Id} was auto-blocked: {moderationResult.Reason}",
                    MetadataJson = $"{{\"conversationId\":\"{conversation.Id}\"}}"
                });

                await _context.SaveChangesAsync(cancellationToken);

                return Result<Guid>.Failure(
                    $"Message blocked: {moderationResult.Reason} Sharing contact or account details is not allowed — all communication and payment must stay on FashionCommerce. This conversation has been flagged for admin review.");
            }
        }

        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = request.SenderId,
            SenderType = senderType,
            Type = Enum.Parse<MessageType>(request.Type),
            Content = request.Content,
            AttachmentUrl = request.AttachmentUrl,
            SentAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        conversation.LastMessageAt = message.SentAt;
        conversation.Status = senderType == MessageSenderType.Customer
            ? ConversationStatus.AwaitingVendorReply
            : ConversationStatus.AwaitingCustomerReply;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(message.Id);
    }
}
