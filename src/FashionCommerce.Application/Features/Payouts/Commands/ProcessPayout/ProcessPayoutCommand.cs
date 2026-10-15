using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Payouts;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Payouts.Commands.ProcessPayout;

/// <summary>
/// Marks a payout as paid once Admin has actually sent the bank transfer.
/// Automating the transfer itself (e.g. via Paystack Transfers API) is a
/// clear next step, but is deliberately NOT done here — moving real money
/// to a vendor's bank account is exactly the kind of action that should
/// stay a deliberate, auditable Admin decision rather than a background job
/// for now, even though the rest of the platform is otherwise automated.
/// </summary>
public record ProcessPayoutCommand : IRequest<Result>, IAuditableRequest
{
    public Guid PayoutRequestId { get; init; }
    public string ExternalTransferReference { get; init; } = default!;
    public Guid ProcessedByAdminId { get; init; }

    public string Action => "PayoutRequest.Processed";
    public string EntityName => "PayoutRequest";
}

public class ProcessPayoutCommandValidator : AbstractValidator<ProcessPayoutCommand>
{
    public ProcessPayoutCommandValidator()
    {
        RuleFor(x => x.PayoutRequestId).NotEmpty();
        RuleFor(x => x.ExternalTransferReference).NotEmpty().MaximumLength(200);
    }
}

public class ProcessPayoutCommandHandler : IRequestHandler<ProcessPayoutCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public ProcessPayoutCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(ProcessPayoutCommand request, CancellationToken cancellationToken)
    {
        var payout = await _context.PayoutRequests.FirstOrDefaultAsync(p => p.Id == request.PayoutRequestId, cancellationToken);
        if (payout is null)
            throw new NotFoundException(nameof(PayoutRequest), request.PayoutRequestId);

        if (payout.Status != PayoutRequestStatus.Requested && payout.Status != PayoutRequestStatus.Approved)
            return Result.Failure($"Payout cannot be processed from status '{payout.Status}'.");

        payout.Status = PayoutRequestStatus.Paid;
        payout.ExternalTransferReference = request.ExternalTransferReference;
        payout.ProcessedAt = DateTime.UtcNow;
        payout.ProcessedByAdminId = request.ProcessedByAdminId;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
