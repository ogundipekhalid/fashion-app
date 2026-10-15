using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Payments;
using FashionCommerce.Domain.Entities.Payouts;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Payments.Commands.ConfirmPayment;

/// <summary>
/// Called from two places that both converge here: (1) the client hitting
/// this after the payment provider redirects back post-checkout, and (2)
/// the provider's webhook (server-to-server, more reliable — the redirect
/// can be closed by the user before completing). Idempotent: verifying an
/// already-Successful payment is a safe no-op, so both callers can race
/// without double-crediting a vendor's wallet.
/// </summary>
public record ConfirmPaymentCommand(string ProviderReference) : IRequest<Result>, IAuditableRequest
{
    public string Action => "Payment.Confirmed";
    public string EntityName => "Payment";
}

public class ConfirmPaymentCommandValidator : AbstractValidator<ConfirmPaymentCommand>
{
    public ConfirmPaymentCommandValidator()
    {
        RuleFor(x => x.ProviderReference).NotEmpty();
    }
}

public class ConfirmPaymentCommandHandler : IRequestHandler<ConfirmPaymentCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;

    public ConfirmPaymentCommandHandler(IApplicationDbContext context, IPaymentGatewayFactory paymentGatewayFactory)
    {
        _context = context;
        _paymentGatewayFactory = paymentGatewayFactory;
    }

    public async Task<Result> Handle(ConfirmPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _context.Payments
            .Include(p => p.Order).ThenInclude(o => o.Items)
            .FirstOrDefaultAsync(p => p.ProviderReference == request.ProviderReference, cancellationToken);

        if (payment is null)
            throw new NotFoundException(nameof(Payment), request.ProviderReference);

        if (payment.Status == PaymentStatus.Successful)
            return Result.Success(); // idempotent — already processed

        var gateway = _paymentGatewayFactory.Resolve(payment.Provider);
        var verifyResult = await gateway.VerifyAsync(request.ProviderReference, cancellationToken);

        if (!verifyResult.Succeeded)
            return Result.Failure($"Could not verify payment: {verifyResult.ErrorMessage}");

        payment.RawResponseJson = verifyResult.RawResponseJson;

        if (!verifyResult.IsPaid)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = "Provider reports payment not completed.";
            payment.Order.MarkPaymentFailed();
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Failure("Payment was not successful.");
        }

        // Sanity-check the verified amount matches what we expect — catches
        // tampering or a provider-side amount mismatch before crediting anyone.
        if (Math.Abs(verifyResult.AmountPaid - payment.Amount) > 0.01m)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = $"Amount mismatch: expected {payment.Amount}, provider reports {verifyResult.AmountPaid}.";
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Failure("Payment amount does not match the order total. This has been flagged.");
        }

        payment.Status = PaymentStatus.Successful;
        payment.PaidAt = DateTime.UtcNow;
        payment.Order.MarkPaymentSuccessful(); // transitions Order to Paid, raises OrderPlacedEvent

        // This is the commission engine's actual trigger point: money has
        // now genuinely arrived, so each vendor's earning moves into their
        // wallet's Pending balance (released to Available later, once the
        // return window closes — see VendorWallet.ReleaseToAvailable, wired
        // up by a scheduled job in the Payouts module, not built yet).
        var vendorIds = payment.Order.Items.Select(i => i.VendorId).Distinct().ToList();
        var wallets = await _context.VendorWallets.Where(w => vendorIds.Contains(w.VendorId)).ToDictionaryAsync(w => w.VendorId, cancellationToken);

        foreach (var item in payment.Order.Items)
        {
            if (!wallets.TryGetValue(item.VendorId, out var wallet))
                continue; // wallet should always exist (created at vendor registration) — defensive skip rather than a hard failure mid-loop

            wallet.CreditPending(item.VendorEarning);
            item.Status = OrderItemStatus.Confirmed;

            _context.CommissionTransactions.Add(new CommissionTransaction
            {
                OrderItemId = item.Id,
                VendorId = item.VendorId,
                GrossAmount = item.LineSubtotal,
                CommissionRatePercent = item.CommissionRatePercent,
                CommissionAmount = item.CommissionAmount,
                VendorEarning = item.VendorEarning,
                Status = CommissionTransactionStatus.Pending
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
