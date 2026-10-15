using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Payouts;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Payouts.Commands.RejectPayout;

public record RejectPayoutCommand : IRequest<Result>, IAuditableRequest
{
    public Guid PayoutRequestId { get; init; }
    public string Reason { get; init; } = default!;

    public string Action => "PayoutRequest.Rejected";
    public string EntityName => "PayoutRequest";
}

public class RejectPayoutCommandValidator : AbstractValidator<RejectPayoutCommand>
{
    public RejectPayoutCommandValidator()
    {
        RuleFor(x => x.PayoutRequestId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class RejectPayoutCommandHandler : IRequestHandler<RejectPayoutCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public RejectPayoutCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(RejectPayoutCommand request, CancellationToken cancellationToken)
    {
        var payout = await _context.PayoutRequests.FirstOrDefaultAsync(p => p.Id == request.PayoutRequestId, cancellationToken);
        if (payout is null)
            throw new NotFoundException(nameof(PayoutRequest), request.PayoutRequestId);

        if (payout.Status != PayoutRequestStatus.Requested)
            return Result.Failure($"Payout cannot be rejected from status '{payout.Status}'.");

        var wallet = await _context.VendorWallets.FirstOrDefaultAsync(w => w.VendorId == payout.VendorId, cancellationToken);
        if (wallet is null)
            return Result.Failure("Vendor wallet not found.");

        // Reverse the debit taken when the payout was originally requested.
        wallet.AvailableBalance += payout.Amount;
        wallet.WithdrawnBalance -= payout.Amount;

        payout.Status = PayoutRequestStatus.Rejected;
        payout.FailureReason = request.Reason;
        payout.ProcessedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
