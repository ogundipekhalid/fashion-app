using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Vendors.Queries.GetVendorDashboardSummary;

public record VendorDashboardSummaryDto(
    decimal TotalRevenue, int TotalOrders, int PendingOrders,
    int TotalProducts, int ActiveProducts, int PendingApprovalProducts,
    decimal AverageRating, int TotalReviews,
    decimal PendingBalance, decimal AvailableBalance,
    int OpenConversations, int PendingRefunds);

public record GetVendorDashboardSummaryQuery(Guid VendorId) : IRequest<VendorDashboardSummaryDto>;

public class GetVendorDashboardSummaryQueryHandler : IRequestHandler<GetVendorDashboardSummaryQuery, VendorDashboardSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetVendorDashboardSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VendorDashboardSummaryDto> Handle(GetVendorDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var vendorItems = _context.OrderItems.Where(i => i.VendorId == request.VendorId);

        var totalRevenue = await vendorItems
            .Where(i => i.Status != OrderItemStatus.Cancelled)
            .SumAsync(i => i.VendorEarning, cancellationToken);

        var totalOrders = await vendorItems.Select(i => i.OrderId).Distinct().CountAsync(cancellationToken);
        var pendingOrders = await vendorItems.CountAsync(i =>
            i.Status == OrderItemStatus.Pending ||
            i.Status == OrderItemStatus.Confirmed ||
            i.Status == OrderItemStatus.Processing, cancellationToken);

        var products = _context.Products.Where(p => p.VendorId == request.VendorId);
        var totalProducts = await products.CountAsync(cancellationToken);
        var activeProducts = await products.CountAsync(p => p.Status == ProductStatus.Active, cancellationToken);
        var pendingApproval = await products.CountAsync(p => p.Status == ProductStatus.PendingApproval, cancellationToken);

        var avgRating = await products.Where(p => p.ReviewCount > 0).Select(p => (decimal?)p.AverageRating).AverageAsync(cancellationToken) ?? 0;
        var totalReviews = await products.SumAsync(p => p.ReviewCount, cancellationToken);

        var wallet = await _context.VendorWallets.FirstOrDefaultAsync(w => w.VendorId == request.VendorId, cancellationToken);

        var openConversations = await _context.Conversations
            .CountAsync(c => c.VendorId == request.VendorId && c.Status != ConversationStatus.Closed && c.Status != ConversationStatus.Blocked, cancellationToken);

        var pendingRefunds = await _context.RefundRequests
            .CountAsync(r => r.VendorId == request.VendorId
                && r.Status != RefundStatus.Refunded && r.Status != RefundStatus.Closed
                && r.Status != RefundStatus.RejectedByAdmin && r.Status != RefundStatus.RejectedByVendor, cancellationToken);

        return new VendorDashboardSummaryDto(
            totalRevenue, totalOrders, pendingOrders,
            totalProducts, activeProducts, pendingApproval,
            Math.Round(avgRating, 2), totalReviews,
            wallet?.PendingBalance ?? 0, wallet?.AvailableBalance ?? 0,
            openConversations, pendingRefunds);
    }
}
