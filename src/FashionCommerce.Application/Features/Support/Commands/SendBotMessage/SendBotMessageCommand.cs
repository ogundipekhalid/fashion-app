using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Support.DTOs;
using FashionCommerce.Domain.Entities.Support;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Support.Commands.SendBotMessage;

public record SendBotMessageCommand : IRequest<Result<BotMessageDto>>
{
    public Guid BotConversationId { get; init; }
    public string Content { get; init; } = default!;

    /// <summary>If the user explicitly confirms they want to escalate (e.g. tapping "Yes, open a ticket" after the bot offered), the caller sets this so the handler skips straight to escalation.</summary>
    public bool ForceEscalate { get; init; }
}

public class SendBotMessageCommandValidator : AbstractValidator<SendBotMessageCommand>
{
    public SendBotMessageCommandValidator()
    {
        RuleFor(x => x.BotConversationId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
    }
}

public class SendBotMessageCommandHandler : IRequestHandler<SendBotMessageCommand, Result<BotMessageDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IBotReplyService _botReplyService;
    private readonly IAiService _aiService;

    public SendBotMessageCommandHandler(IApplicationDbContext context, IBotReplyService botReplyService, IAiService aiService)
    {
        _context = context;
        _botReplyService = botReplyService;
        _aiService = aiService;
    }

    public async Task<Result<BotMessageDto>> Handle(SendBotMessageCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _context.BotConversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == request.BotConversationId, cancellationToken);

        if (conversation is null)
            throw new NotFoundException(nameof(BotConversation), request.BotConversationId);

        if (conversation.EndedAt.HasValue)
            return Result<BotMessageDto>.Failure("This conversation has ended. Start a new one to continue.");

        conversation.Messages.Add(new BotMessage
        {
            BotConversationId = conversation.Id,
            Sender = BotMessageSender.User,
            Content = request.Content
        });

        // The styling assistant is genuinely conversational (real AI, per the
        // spec's "AI Chat Assistant" / "AI Styling Assistant" features) — it
        // never escalates to a support ticket, unlike the rule-based
        // support/dev-contact bots which exist specifically to catch and
        // route real issues to a human.
        if (conversation.Channel == BotChannel.AiStylingAssistant)
        {
            var history = conversation.Messages
                .Where(m => m.Sender != BotMessageSender.User || m.Content != request.Content) // exclude the message just added above from its own history
                .OrderBy(m => m.SentAt)
                .Select(m => $"{m.Sender}: {m.Content}")
                .ToList();

            var aiResult = await _aiService.GetStylingAdviceAsync(request.Content, history, cancellationToken);

            var aiReplyContent = aiResult.Succeeded
                ? aiResult.Text!
                : "Sorry, I'm having trouble giving styling advice right now — please try again in a moment.";

            var aiBotMessage = new BotMessage
            {
                BotConversationId = conversation.Id,
                Sender = BotMessageSender.Bot,
                Content = aiReplyContent,
                Intent = "styling_advice"
            };

            conversation.Messages.Add(aiBotMessage);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<BotMessageDto>.Success(new BotMessageDto(aiBotMessage.Sender.ToString(), aiBotMessage.Content, aiBotMessage.Intent, aiBotMessage.SentAt));
        }

        var reply = _botReplyService.Reply(request.Content, conversation.Channel.ToString());
        var shouldEscalate = request.ForceEscalate || reply.ShouldEscalateToTicket;

        BotMessage botMessage;

        if (shouldEscalate)
        {
            var ticket = new SupportTicket
            {
                TicketNumber = $"TCK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                RequesterId = conversation.UserId,
                RequesterRole = conversation.UserRole,
                Category = reply.DetectedIntent == "report_bug" ? SupportTicketCategory.BugReport : SupportTicketCategory.TechnicalIssue,
                Priority = SupportTicketPriority.Normal,
                Subject = $"Escalated from bot conversation ({conversation.Channel})",
                Description = BuildTranscript(conversation)
            };

            _context.SupportTickets.Add(ticket);
            conversation.EscalateToTicket(ticket.Id);

            botMessage = new BotMessage
            {
                BotConversationId = conversation.Id,
                Sender = BotMessageSender.Bot,
                Content = $"{reply.Content} I've opened ticket {ticket.TicketNumber} for you — our team will follow up soon.",
                Intent = reply.DetectedIntent,
                ConfidenceScore = reply.ConfidenceScore
            };
        }
        else
        {
            botMessage = new BotMessage
            {
                BotConversationId = conversation.Id,
                Sender = BotMessageSender.Bot,
                Content = reply.Content,
                Intent = reply.DetectedIntent,
                ConfidenceScore = reply.ConfidenceScore
            };
        }

        conversation.Messages.Add(botMessage);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<BotMessageDto>.Success(new BotMessageDto(botMessage.Sender.ToString(), botMessage.Content, botMessage.Intent, botMessage.SentAt));
    }

    private static string BuildTranscript(BotConversation conversation) =>
        string.Join("\n", conversation.Messages.OrderBy(m => m.SentAt).Select(m => $"[{m.Sender}] {m.Content}"));
}
