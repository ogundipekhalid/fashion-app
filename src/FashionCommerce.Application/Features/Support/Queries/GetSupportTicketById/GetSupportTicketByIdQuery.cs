using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Support.DTOs;
using FashionCommerce.Domain.Entities.Support;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Support.Queries.GetSupportTicketById;

public record GetSupportTicketByIdQuery(Guid SupportTicketId, Guid RequesterId, UserRole RequesterRole) : IRequest<SupportTicketDto>;

public class GetSupportTicketByIdQueryHandler : IRequestHandler<GetSupportTicketByIdQuery, SupportTicketDto>
{
    private readonly IApplicationDbContext _context;

    public GetSupportTicketByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SupportTicketDto> Handle(GetSupportTicketByIdQuery request, CancellationToken cancellationToken)
    {
        var ticket = await _context.SupportTickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == request.SupportTicketId, cancellationToken);

        if (ticket is null)
            throw new NotFoundException(nameof(SupportTicket), request.SupportTicketId);

        var isRequester = ticket.RequesterId == request.RequesterId && ticket.RequesterRole == request.RequesterRole;
        var isStaff = request.RequesterRole is UserRole.Admin or UserRole.SuperAdmin or UserRole.SupportAgent;

        if (!isRequester && !isStaff)
            throw new ForbiddenAccessException("You do not have access to this support ticket.");

        return new SupportTicketDto(
            ticket.Id, ticket.TicketNumber, ticket.Category.ToString(), ticket.Priority.ToString(), ticket.Status.ToString(),
            ticket.Subject, ticket.Description, ticket.AssignedAgentId,
            ticket.CreatedAt, ticket.ResolvedAt, ticket.ResolutionNote,
            ticket.Messages.OrderBy(m => m.SentAt)
                .Select(m => new SupportTicketMessageDto(m.Id, m.SenderId, m.SenderRole.ToString(), m.Content, m.SentAt))
                .ToList());
    }
}
