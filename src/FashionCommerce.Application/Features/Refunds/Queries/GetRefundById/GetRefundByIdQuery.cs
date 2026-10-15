using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Refunds.DTOs;
using FashionCommerce.Domain.Entities.Refunds;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Refunds.Queries.GetRefundById;

public record GetRefundByIdQuery(Guid RefundRequestId, Guid RequesterProfileId, UserRole RequesterRole) : IRequest<RefundRequestDto>;

public class GetRefundByIdQueryHandler : IRequestHandler<GetRefundByIdQuery, RefundRequestDto>
{
    private readonly IApplicationDbContext _context;

    public GetRefundByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RefundRequestDto> Handle(GetRefundByIdQuery request, CancellationToken cancellationToken)
    {
        var refund = await _context.RefundRequests
            .Include(r => r.Evidence)
            .Include(r => r.AuditLogs)
            .FirstOrDefaultAsync(r => r.Id == request.RefundRequestId, cancellationToken);

        if (refund is null)
            throw new NotFoundException(nameof(RefundRequest), request.RefundRequestId);

        var isOwner = request.RequesterRole == UserRole.Customer && refund.CustomerId == request.RequesterProfileId;
        var isVendor = request.RequesterRole == UserRole.Vendor && refund.VendorId == request.RequesterProfileId;
        var isAdmin = request.RequesterRole is UserRole.Admin or UserRole.SuperAdmin or UserRole.SupportAgent;

        if (!isOwner && !isVendor && !isAdmin)
            throw new ForbiddenAccessException("You do not have access to this refund request.");

        var orderItem = await _context.OrderItems.FirstOrDefaultAsync(i => i.Id == refund.OrderItemId, cancellationToken);

        return new RefundRequestDto(
            refund.Id, refund.OrderItemId, refund.OrderId, orderItem?.ProductNameSnapshot ?? "Unknown product",
            refund.Type.ToString(), refund.RequestedAmount, refund.ApprovedAmount,
            refund.Reason, refund.CustomerNote, refund.VendorResponseNote, refund.AdminDecisionNote,
            refund.Status.ToString(), refund.WasVendorAlreadyPaidOut,
            refund.Evidence.Select(e => new RefundEvidenceDto(e.Id, e.Url, e.IsVideo, e.UploadedByRole)).ToList(),
            refund.AuditLogs.OrderBy(a => a.Timestamp).Select(a => new RefundAuditLogDto(a.Actor.ToString(), a.Message, a.Timestamp)).ToList(),
            refund.CreatedAt);
    }
}
