using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Chat;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Chat.Commands.ReportConversation;

public record ReportConversationCommand : IRequest<Result>, IAuditableRequest
{
    public Guid ConversationId { get; init; }
    public Guid ReportedBy { get; init; }
    public string Reason { get; init; } = default!;

    public string Action => "Conversation.Reported";
    public string EntityName => "Conversation";
}

public class ReportConversationCommandValidator : AbstractValidator<ReportConversationCommand>
{
    public ReportConversationCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public class ReportConversationCommandHandler : IRequestHandler<ReportConversationCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public ReportConversationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(ReportConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == request.ConversationId, cancellationToken);
        if (conversation is null)
            throw new NotFoundException(nameof(Conversation), request.ConversationId);

        conversation.Report(request.ReportedBy, request.Reason);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
