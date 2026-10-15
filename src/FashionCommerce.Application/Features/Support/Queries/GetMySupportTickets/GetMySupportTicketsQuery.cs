using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Support.DTOs;
using FashionCommerce.Domain.Entities.Support;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Support.Queries.GetMySupportTickets;

public record GetMySupportTicketsQuery : IRequest<PaginatedList<SupportTicketSummaryDto>>
{
    public Guid RequesterId { get; init; }
    public UserRole RequesterRole { get; init; }

    /// <summary>Staff-only: when true (and the requester is Admin/SuperAdmin/SupportAgent), returns ALL tickets rather than just the requester's own.</summary>
    public bool StaffViewAll { get; init; }
    public string? StatusFilter { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetMySupportTicketsQueryHandler : IRequestHandler<GetMySupportTicketsQuery, PaginatedList<SupportTicketSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMySupportTicketsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<SupportTicketSummaryDto>> Handle(GetMySupportTicketsQuery request, CancellationToken cancellationToken)
    {
        var isStaff = request.RequesterRole is UserRole.Admin or UserRole.SuperAdmin or UserRole.SupportAgent;

        var query = isStaff && request.StaffViewAll
            ? _context.SupportTickets.AsQueryable()
            : _context.SupportTickets.Where(t => t.RequesterId == request.RequesterId && t.RequesterRole == request.RequesterRole);

        if (!string.IsNullOrWhiteSpace(request.StatusFilter) && Enum.TryParse<SupportTicketStatus>(request.StatusFilter, out var status))
            query = query.Where(t => t.Status == status);

        var projected = query.OrderByDescending(t => t.CreatedAt)
            .Select(t => new SupportTicketSummaryDto(t.Id, t.TicketNumber, t.Category.ToString(), t.Priority.ToString(), t.Status.ToString(), t.Subject, t.CreatedAt));

        return await PaginatedList<SupportTicketSummaryDto>.CreateAsync(projected, request.PageNumber, request.PageSize);
    }
}
