using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Support;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FashionCommerce.Application.Features.Support.Commands.StartBotConversation;

public record StartBotConversationCommand : IRequest<Result<Guid>>
{
    public Guid UserId { get; init; }
    public string UserRole { get; init; } = default!;
    public string Channel { get; init; } = default!; // "AiStylingAssistant" | "CustomerSupportBot" | "DeveloperContactBot"
}

public class StartBotConversationCommandValidator : AbstractValidator<StartBotConversationCommand>
{
    public StartBotConversationCommandValidator()
    {
        RuleFor(x => x.Channel).Must(c => Enum.TryParse<BotChannel>(c, out _)).WithMessage("Invalid bot channel.");
    }
}

public class StartBotConversationCommandHandler : IRequestHandler<StartBotConversationCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public StartBotConversationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(StartBotConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = new BotConversation
        {
            UserId = request.UserId,
            UserRole = Enum.Parse<UserRole>(request.UserRole),
            Channel = Enum.Parse<BotChannel>(request.Channel)
        };

        conversation.Messages.Add(new BotMessage
        {
            BotConversationId = conversation.Id,
            Sender = BotMessageSender.Bot,
            Content = GreetingFor(conversation.Channel)
        });

        _context.BotConversations.Add(conversation);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(conversation.Id);
    }

    private static string GreetingFor(BotChannel channel) => channel switch
    {
        BotChannel.DeveloperContactBot => "Hi! I'm here to help you report an issue or get in touch with the FashionCommerce team. What's going on?",
        BotChannel.CustomerSupportBot => "Hi! How can I help you today?",
        BotChannel.AiStylingAssistant => "Hi! Tell me what you're shopping for and I'll help you find something to style it with.",
        _ => "Hi! How can I help?"
    };
}
