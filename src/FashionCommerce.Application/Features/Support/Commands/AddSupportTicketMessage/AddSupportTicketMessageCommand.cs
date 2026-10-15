using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Support;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Support.Commands.AddSupportTicketMessage;

public record AddSupportTicketMessageCommand : IRequest<Result>
{
    public Guid SupportTicketId { get; init; }
    public Guid SenderId { get; init; }
    public string SenderRole { get; init; } = default!;
    public string Content { get; init; } = default!;
}

public class AddSupportTicketMessageCommandValidator : AbstractValidator<AddSupportTicketMessageCommand>
{
    public AddSupportTicketMessageCommandValidator()
    {
        RuleFor(x => x.SupportTicketId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
    }
}

public class AddSupportTicketMessageCommandHandler : IRequestHandler<AddSupportTicketMessageCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public AddSupportTicketMessageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(AddSupportTicketMessageCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == request.SupportTicketId, cancellationToken);
        if (ticket is null)
            throw new NotFoundException(nameof(SupportTicket), request.SupportTicketId);

        var senderRole = Enum.Parse<UserRole>(request.SenderRole);
        var isRequester = ticket.RequesterId == request.SenderId && ticket.RequesterRole == senderRole;
        var isStaff = senderRole is UserRole.Admin or UserRole.SuperAdmin or UserRole.SupportAgent;

        if (!isRequester && !isStaff)
            throw new ForbiddenAccessException("You do not have access to this support ticket.");

        _context.SupportTicketMessages.Add(new SupportTicketMessage
        {
            SupportTicketId = ticket.Id,
            SenderId = request.SenderId,
            SenderRole = senderRole,
            Content = request.Content
        });

        // A reply from the requester after resolution reopens the ticket automatically.
        if (isRequester && ticket.Status == SupportTicketStatus.Resolved)
            ticket.Reopen();
        else if (isStaff && ticket.Status == SupportTicketStatus.Open)
            ticket.Status = SupportTicketStatus.InProgress;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
