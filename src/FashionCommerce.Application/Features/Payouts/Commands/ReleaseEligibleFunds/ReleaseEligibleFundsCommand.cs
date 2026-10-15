using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Payouts.Commands.ReleaseEligibleFunds;

/// <summary>
/// Moves commission earnings from Pending to Available once they're past
/// the platform's return/dispute window. This SHOULD run automatically on
/// a schedule (a Hangfire recurring job, per the spec) — that job isn't
/// wired up yet, so this command exists as an Admin-triggerable stand-in.
/// The actual release logic itself is complete and correct; only the
/// "runs automatically every night" part is missing.
/// </summary>
public record ReleaseEligibleFundsCommand : IRequest<Result<int>>, IAuditableRequest
{
    public int ReturnWindowDays { get; init; } = 7;

    public string Action => "VendorWallet.FundsReleased";
    public string EntityName => "VendorWallet";
}

public class ReleaseEligibleFundsCommandHandler : IRequestHandler<ReleaseEligibleFundsCommand, Result<int>>
{
    private readonly IApplicationDbContext _context;

    public ReleaseEligibleFundsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> Handle(ReleaseEligibleFundsCommand request, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-request.ReturnWindowDays);

        var eligibleTransactions = await _context.CommissionTransactions
            .Where(t => t.Status == CommissionTransactionStatus.Pending && t.CreatedAt <= cutoff)
            .ToListAsync(cancellationToken);

        if (eligibleTransactions.Count == 0)
            return Result<int>.Success(0);

        var vendorIds = eligibleTransactions.Select(t => t.VendorId).Distinct().ToList();
        var wallets = await _context.VendorWallets
            .Where(w => vendorIds.Contains(w.VendorId))
            .ToDictionaryAsync(w => w.VendorId, cancellationToken);

        var releasedCount = 0;
        foreach (var transaction in eligibleTransactions)
        {
            if (!wallets.TryGetValue(transaction.VendorId, out var wallet))
                continue;

            try
            {
                wallet.ReleaseToAvailable(transaction.VendorEarning);
            }
            catch (Domain.Exceptions.DomainValidationException)
            {
                continue; // wallet's pending balance doesn't cover this transaction (e.g. already adjusted by a refund) — skip rather than abort the whole batch
            }

            transaction.Status = CommissionTransactionStatus.Settled;
            transaction.SettledAt = DateTime.UtcNow;
            releasedCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(releasedCount);
    }
}
