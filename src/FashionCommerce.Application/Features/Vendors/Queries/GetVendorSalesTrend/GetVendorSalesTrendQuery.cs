using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Admin.Queries.GetRevenueTrend;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Vendors.Queries.GetVendorSalesTrend;

public record GetVendorSalesTrendQuery : IRequest<List<RevenueTrendPointDto>>
{
    public Guid VendorId { get; init; }
    public DateTime FromDate { get; init; } = DateTime.UtcNow.AddDays(-30);
    public DateTime ToDate { get; init; } = DateTime.UtcNow;
    public TrendGranularity Granularity { get; init; } = TrendGranularity.Daily;
}

public class GetVendorSalesTrendQueryHandler : IRequestHandler<GetVendorSalesTrendQuery, List<RevenueTrendPointDto>>
{
    private readonly IApplicationDbContext _context;

    public GetVendorSalesTrendQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<RevenueTrendPointDto>> Handle(GetVendorSalesTrendQuery request, CancellationToken cancellationToken)
    {
        // Vendor earnings, not gross order total — a vendor's "sales" figure
        // should reflect what THEY earn (post-commission), not the full
        // customer-paid amount which includes the platform's cut.
        var items = await _context.OrderItems
            .Where(i => i.VendorId == request.VendorId)
            .Where(i => i.Status != OrderItemStatus.Cancelled)
            .Where(i => i.CreatedAt >= request.FromDate && i.CreatedAt <= request.ToDate)
            .Select(i => new { i.CreatedAt, i.VendorEarning })
            .ToListAsync(cancellationToken);

        return TrendBucketer.Bucket(items.Select(i => (i.CreatedAt, i.VendorEarning)), request.Granularity);
    }
}
