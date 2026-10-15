using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Chat;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Chat.Commands.StartConversation;

public record StartConversationCommand : IRequest<Result<Guid>>, IAuditableRequest
{
    /// <summary>Resolved server-side from ICurrentUserService.</summary>
    public Guid CustomerId { get; init; }

    public Guid ProductId { get; init; }
    public string Type { get; init; } = default!; // ConversationType as string, e.g. "ProductEnquiry"
    public Guid? OrderId { get; init; }
    public Guid? RefundRequestId { get; init; }
    public string? Subject { get; init; }
    public string InitialMessage { get; init; } = default!;

    public string Action => "Conversation.Started";
    public string EntityName => "Conversation";
}

public class StartConversationCommandValidator : AbstractValidator<StartConversationCommand>
{
    public StartConversationCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Type).Must(t => Enum.TryParse<ConversationType>(t, out _)).WithMessage("Invalid conversation type.");
        RuleFor(x => x.InitialMessage).NotEmpty().MaximumLength(4000);
    }
}

public class StartConversationCommandHandler : IRequestHandler<StartConversationCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IChatModerationService _moderation;

    public StartConversationCommandHandler(IApplicationDbContext context, IChatModerationService moderation)
    {
        _context = context;
        _moderation = moderation;
    }

    public async Task<Result<Guid>> Handle(StartConversationCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product is null)
            throw new NotFoundException(nameof(Domain.Entities.Catalog.Product), request.ProductId);

        // The initial message is checked BEFORE the conversation is even created —
        // no point opening a thread that gets immediately auto-blocked.
        var moderationResult = _moderation.CheckMessage(request.InitialMessage, Array.Empty<string>());
        if (moderationResult.IsSuspicious)
            return Result<Guid>.Failure($"Message not sent: {moderationResult.Reason} Sharing contact or account details is not allowed on FashionCommerce chat — all communication and payment must stay on the platform.");

        var conversation = Domain.Entities.Chat.Conversation.Start(
            request.CustomerId, product.VendorId, request.ProductId,
            Enum.Parse<ConversationType>(request.Type), request.OrderId, request.RefundRequestId, request.Subject);

        conversation.Messages.Add(new Message
        {
            ConversationId = conversation.Id,
            SenderId = request.CustomerId,
            SenderType = MessageSenderType.Customer,
            Type = MessageType.Text,
            Content = request.InitialMessage,
            SentAt = DateTime.UtcNow
        });
        conversation.LastMessageAt = DateTime.UtcNow;

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(conversation.Id);
    }
}
