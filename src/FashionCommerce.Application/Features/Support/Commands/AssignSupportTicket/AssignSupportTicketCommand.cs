using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Support;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Support.Commands.AssignSupportTicket;

public record AssignSupportTicketCommand : IRequest<Result>, IAuditableRequest
{
    public Guid SupportTicketId { get; init; }
    public Guid AgentId { get; init; }

    public string Action => "SupportTicket.Assigned";
    public string EntityName => "SupportTicket";
}

public class AssignSupportTicketCommandValidator : AbstractValidator<AssignSupportTicketCommand>
{
    public AssignSupportTicketCommandValidator()
    {
        RuleFor(x => x.SupportTicketId).NotEmpty();
        RuleFor(x => x.AgentId).NotEmpty();
    }
}

public class AssignSupportTicketCommandHandler : IRequestHandler<AssignSupportTicketCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public AssignSupportTicketCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(AssignSupportTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == request.SupportTicketId, cancellationToken);
        if (ticket is null)
            throw new NotFoundException(nameof(SupportTicket), request.SupportTicketId);

        var agentExists = await _context.Admins.AnyAsync(a => a.Id == request.AgentId, cancellationToken);
        if (!agentExists)
            return Result.Failure("Agent not found.");

        ticket.AssignedAgentId = request.AgentId;
        if (ticket.Status == Domain.Enums.SupportTicketStatus.Open)
            ticket.Status = Domain.Enums.SupportTicketStatus.InProgress;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
