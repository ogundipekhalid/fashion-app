using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Admin.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Admin.Queries.GetAuditLogs;

public record GetAuditLogsQuery : IRequest<PaginatedList<AuditLogEntryDto>>
{
    public string? EntityNameFilter { get; init; }
    public Guid? UserIdFilter { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, PaginatedList<AuditLogEntryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<AuditLogEntryDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.EntityNameFilter))
            query = query.Where(a => a.EntityName == request.EntityNameFilter);

        if (request.UserIdFilter.HasValue)
            query = query.Where(a => a.UserId == request.UserIdFilter);

        if (request.FromDate.HasValue)
            query = query.Where(a => a.Timestamp >= request.FromDate);

        if (request.ToDate.HasValue)
            query = query.Where(a => a.Timestamp <= request.ToDate);

        var projected = query.OrderByDescending(a => a.Timestamp).Select(a => new AuditLogEntryDto(
            a.Id, a.UserId, a.UserRole, a.Action, a.EntityName, a.EntityId, a.IpAddress, a.Timestamp));

        return await PaginatedList<AuditLogEntryDto>.CreateAsync(projected, request.PageNumber, request.PageSize);
    }
}
