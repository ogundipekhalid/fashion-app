using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Refunds.Queries.GetMyRefunds;

public record RefundSummaryDto(Guid Id, Guid OrderId, string Status, decimal RequestedAmount, decimal? ApprovedAmount, DateTime CreatedAt);

public record GetMyRefundsQuery : IRequest<PaginatedList<RefundSummaryDto>>
{
    public Guid RequesterProfileId { get; init; }
    public UserRole RequesterRole { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetMyRefundsQueryHandler : IRequestHandler<GetMyRefundsQuery, PaginatedList<RefundSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyRefundsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<RefundSummaryDto>> Handle(GetMyRefundsQuery request, CancellationToken cancellationToken)
    {
        var query = request.RequesterRole == UserRole.Vendor
            ? _context.RefundRequests.Where(r => r.VendorId == request.RequesterProfileId)
            : _context.RefundRequests.Where(r => r.CustomerId == request.RequesterProfileId);

        var projected = query.OrderByDescending(r => r.CreatedAt)
            .Select(r => new RefundSummaryDto(r.Id, r.OrderId, r.Status.ToString(), r.RequestedAmount, r.ApprovedAmount, r.CreatedAt));

        return await PaginatedList<RefundSummaryDto>.CreateAsync(projected, request.PageNumber, request.PageSize);
    }
}
