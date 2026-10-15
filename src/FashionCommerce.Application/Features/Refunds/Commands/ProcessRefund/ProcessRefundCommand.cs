using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Orders;
using FashionCommerce.Domain.Entities.Payments;
using FashionCommerce.Domain.Entities.Refunds;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Refunds.Commands.ProcessRefund;

/// <summary>
/// The final step: takes an Admin- or Vendor-approved RefundRequest, issues
/// the actual refund with the payment provider, and applies the correct
/// VendorWallet clawback depending on whether the vendor had already been
/// paid out for that item — the platform's core refund policy rule.
/// </summary>
public record ProcessRefundCommand(Guid RefundRequestId) : IRequest<Result>, IAuditableRequest
{
    public string Action => "RefundRequest.Processed";
    public string EntityName => "RefundRequest";
}

public class ProcessRefundCommandValidator : AbstractValidator<ProcessRefundCommand>
{
    public ProcessRefundCommandValidator()
    {
        RuleFor(x => x.RefundRequestId).NotEmpty();
    }
}

public class ProcessRefundCommandHandler : IRequestHandler<ProcessRefundCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;

    public ProcessRefundCommandHandler(IApplicationDbContext context, IPaymentGatewayFactory paymentGatewayFactory)
    {
        _context = context;
        _paymentGatewayFactory = paymentGatewayFactory;
    }

    public async Task<Result> Handle(ProcessRefundCommand request, CancellationToken cancellationToken)
    {
        var refund = await _context.RefundRequests.FirstOrDefaultAsync(r => r.Id == request.RefundRequestId, cancellationToken);
        if (refund is null)
            throw new NotFoundException(nameof(RefundRequest), request.RefundRequestId);

        if (refund.Status is not (RefundStatus.ApprovedByVendor or RefundStatus.ApprovedByAdmin))
            return Result.Failure($"Refund must be approved before processing. Current status: {refund.Status}.");

        var orderItem = await _context.OrderItems.FirstOrDefaultAsync(i => i.Id == refund.OrderItemId, cancellationToken);
        if (orderItem is null)
            return Result.Failure("The original order item could not be found.");

        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == refund.OrderId && p.Status == PaymentStatus.Successful, cancellationToken);
        if (payment is null)
            return Result.Failure("No successful payment found for this order to refund against.");

        var refundAmount = refund.ApprovedAmount ?? refund.RequestedAmount;

        try
        {
            refund.MarkProcessing();
        }
        catch (RefundNotEligibleException ex)
        {
            return Result.Failure(ex.Message);
        }

        var gateway = _paymentGatewayFactory.Resolve(payment.Provider);
        var gatewayResult = await gateway.RefundAsync(payment.ProviderReference, refundAmount, refund.Reason, cancellationToken);

        if (!gatewayResult.Succeeded)
            return Result.Failure($"Refund could not be processed with the payment provider: {gatewayResult.ErrorMessage}");

        // Apply the vendor wallet clawback per the platform's refund policy —
        // this is the rule from the spec: before payout, refund comes off the
        // still-pending balance; after payout, it's clawed back from
        // Available (possibly going negative, offsetting future earnings).
        var wallet = await _context.VendorWallets.FirstOrDefaultAsync(w => w.VendorId == refund.VendorId, cancellationToken);
        if (wallet is not null)
        {
            // Only the vendor's share of the refunded amount is clawed back —
            // the commission portion's fate depends on CommissionPolicy, applied separately below.
            var vendorShareOfRefund = orderItem.LineSubtotal > 0
                ? Math.Round(refundAmount * (orderItem.VendorEarning / orderItem.LineSubtotal), 2)
                : 0;

            if (refund.WasVendorAlreadyPaidOut)
                wallet.ApplyPostPayoutRefundClawback(vendorShareOfRefund);
            else
                wallet.ApplyPrePayoutRefund(vendorShareOfRefund);
        }

        orderItem.Status = OrderItemStatus.Refunded;

        // Full refund on the only/last remaining item effectively refunds the order;
        // partial-item refunds leave Order.Status as-is (still Delivered/Processing/etc.)
        var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == refund.OrderId, cancellationToken);
        if (order is not null && order.Items.All(i => i.Status == OrderItemStatus.Refunded))
            payment.Status = PaymentStatus.Refunded;
        else if (payment.Status == PaymentStatus.Successful)
            payment.Status = PaymentStatus.PartiallyRefunded;

        refund.MarkRefunded(refundAmount);

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
