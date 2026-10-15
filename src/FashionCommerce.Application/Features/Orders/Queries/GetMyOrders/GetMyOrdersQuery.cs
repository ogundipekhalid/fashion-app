using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Orders.DTOs;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Orders.Queries.GetMyOrders;

public record GetMyOrdersQuery : IRequest<PaginatedList<OrderSummaryDto>>
{
    public Guid RequesterProfileId { get; init; }
    public UserRole RequesterRole { get; init; }
    public string? StatusFilter { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetMyOrdersQueryHandler : IRequestHandler<GetMyOrdersQuery, PaginatedList<OrderSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyOrdersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<OrderSummaryDto>> Handle(GetMyOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = request.RequesterRole == UserRole.Vendor
            ? _context.Orders.Where(o => o.Items.Any(i => i.VendorId == request.RequesterProfileId))
            : _context.Orders.Where(o => o.CustomerId == request.RequesterProfileId);

        if (!string.IsNullOrWhiteSpace(request.StatusFilter) && Enum.TryParse<OrderStatus>(request.StatusFilter, out var status))
            query = query.Where(o => o.Status == status);

        var projected = query.OrderByDescending(o => o.PlacedAt).Select(o => new OrderSummaryDto(
            o.Id, o.OrderNumber, o.Status.ToString(), o.PaymentStatus.ToString(),
            o.Total, o.Items.Count, o.PlacedAt));

        return await PaginatedList<OrderSummaryDto>.CreateAsync(projected, request.PageNumber, request.PageSize);
    }
}
