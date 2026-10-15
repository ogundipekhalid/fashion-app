using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Admin.DTOs;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Admin.Queries.GetDashboardSummary;

public record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetDashboardSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var totalRevenue = await _context.Orders
            .Where(o => o.PaymentStatus == PaymentStatus.Successful || o.PaymentStatus == PaymentStatus.PartiallyRefunded)
            .SumAsync(o => o.Total, cancellationToken);

        var totalOrders = await _context.Orders.CountAsync(cancellationToken);
        var totalCustomers = await _context.Customers.CountAsync(cancellationToken);
        var totalVendors = await _context.Vendors.CountAsync(cancellationToken);

        var pendingVendorVerifications = await _context.Vendors
            .CountAsync(v => v.VerificationStatus == VendorVerificationStatus.Pending || v.VerificationStatus == VendorVerificationStatus.UnderReview, cancellationToken);

        var pendingProductApprovals = await _context.Products
            .CountAsync(p => p.Status == ProductStatus.PendingApproval, cancellationToken);

        var openSupportTickets = await _context.SupportTickets
            .CountAsync(t => t.Status == SupportTicketStatus.Open || t.Status == SupportTicketStatus.InProgress || t.Status == SupportTicketStatus.Reopened, cancellationToken);

        var pendingRefunds = await _context.RefundRequests
            .CountAsync(r => r.Status != RefundStatus.Refunded && r.Status != RefundStatus.Closed && r.Status != RefundStatus.RejectedByAdmin && r.Status != RefundStatus.RejectedByVendor, cancellationToken);

        var pendingPayoutRequests = await _context.PayoutRequests
            .CountAsync(p => p.Status == PayoutRequestStatus.Requested, cancellationToken);

        var totalCommissionEarned = await _context.CommissionTransactions
            .Where(c => c.Status == CommissionTransactionStatus.Settled)
            .SumAsync(c => c.CommissionAmount, cancellationToken);

        return new DashboardSummaryDto(
            totalRevenue, totalOrders, totalCustomers, totalVendors,
            pendingVendorVerifications, pendingProductApprovals,
            openSupportTickets, pendingRefunds, pendingPayoutRequests,
            totalCommissionEarned);
    }
}
