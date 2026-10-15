using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Orders;
using FashionCommerce.Domain.Entities.Refunds;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Refunds.Commands.RequestRefund;

public record RequestRefundCommand : IRequest<Result<Guid>>, IAuditableRequest
{
    public Guid OrderItemId { get; init; }
    public Guid RequestingCustomerId { get; init; }
    public decimal RequestedAmount { get; init; }
    public string Reason { get; init; } = default!;
    public string? CustomerNote { get; init; }
    public List<string> EvidenceUrls { get; init; } = new();

    public string Action => "RefundRequest.Created";
    public string EntityName => "RefundRequest";
}

public class RequestRefundCommandValidator : AbstractValidator<RequestRefundCommand>
{
    public RequestRefundCommandValidator()
    {
        RuleFor(x => x.OrderItemId).NotEmpty();
        RuleFor(x => x.RequestedAmount).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public class RequestRefundCommandHandler : IRequestHandler<RequestRefundCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public RequestRefundCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(RequestRefundCommand request, CancellationToken cancellationToken)
    {
        var orderItem = await _context.OrderItems
            .Include(i => i.Order)
            .FirstOrDefaultAsync(i => i.Id == request.OrderItemId, cancellationToken);

        if (orderItem is null)
            throw new NotFoundException(nameof(OrderItem), request.OrderItemId);

        if (orderItem.Order.CustomerId != request.RequestingCustomerId)
            throw new ForbiddenAccessException("This order item does not belong to you.");

        if (orderItem.Status != OrderItemStatus.Delivered)
            return Result<Guid>.Failure("Refunds can only be requested for delivered items.");

        var alreadyHasOpenRefund = await _context.RefundRequests.AnyAsync(
            r => r.OrderItemId == request.OrderItemId && r.Status != RefundStatus.Closed && r.Status != RefundStatus.RejectedByAdmin,
            cancellationToken);

        if (alreadyHasOpenRefund)
            return Result<Guid>.Failure("A refund request already exists for this item.");

        if (request.RequestedAmount > orderItem.LineSubtotal)
            return Result<Guid>.Failure($"Requested amount cannot exceed the item's price ({orderItem.LineSubtotal:N2}).");

        // Whether the vendor's earning on this item has already been released
        // (settled) determines which VendorWallet clawback path applies later,
        // per the platform's refund policy — see VendorWallet.ApplyPrePayoutRefund
        // vs ApplyPostPayoutRefundClawback.
        var commissionTransaction = await _context.CommissionTransactions
            .FirstOrDefaultAsync(c => c.OrderItemId == request.OrderItemId, cancellationToken);
        var vendorAlreadyPaidOut = commissionTransaction?.Status == CommissionTransactionStatus.Settled;

        var type = request.RequestedAmount >= orderItem.LineSubtotal ? RefundType.Full : RefundType.Partial;

        var refundRequest = RefundRequest.Create(
            request.OrderItemId, orderItem.OrderId, request.RequestingCustomerId, orderItem.VendorId,
            type, request.RequestedAmount, request.Reason, vendorAlreadyPaidOut);

        refundRequest.CustomerNote = request.CustomerNote;

        foreach (var url in request.EvidenceUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
        {
            refundRequest.Evidence.Add(new RefundEvidence
            {
                RefundRequestId = refundRequest.Id,
                Url = url,
                UploadedByRole = "Customer"
            });
        }

        orderItem.Status = OrderItemStatus.RefundRequested;

        _context.RefundRequests.Add(refundRequest);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(refundRequest.Id);
    }
}
