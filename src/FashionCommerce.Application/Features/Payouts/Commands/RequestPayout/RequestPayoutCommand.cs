using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Payouts;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Payouts.Commands.RequestPayout;

public record RequestPayoutCommand : IRequest<Result<Guid>>, IAuditableRequest
{
    public Guid VendorId { get; init; }
    public decimal Amount { get; init; }

    public string Action => "PayoutRequest.Created";
    public string EntityName => "PayoutRequest";
}

public class RequestPayoutCommandValidator : AbstractValidator<RequestPayoutCommand>
{
    public RequestPayoutCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public class RequestPayoutCommandHandler : IRequestHandler<RequestPayoutCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public RequestPayoutCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(RequestPayoutCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == request.VendorId, cancellationToken);
        if (vendor is null)
            throw new NotFoundException(nameof(Domain.Entities.Identity.Vendor), request.VendorId);

        if (string.IsNullOrWhiteSpace(vendor.BankAccountNumber) || string.IsNullOrWhiteSpace(vendor.BankName))
            return Result<Guid>.Failure("Please add your bank account details before requesting a payout.");

        var wallet = await _context.VendorWallets.FirstOrDefaultAsync(w => w.VendorId == request.VendorId, cancellationToken);
        if (wallet is null)
            return Result<Guid>.Failure("Wallet not found.");

        if (wallet.AvailableBalance < request.Amount)
            return Result<Guid>.Failure($"Insufficient available balance. Available: {wallet.AvailableBalance:N2}, requested: {request.Amount:N2}.");

        // Debit immediately (moves Available -> Withdrawn) so the same funds
        // can't be requested twice while this request is pending Admin
        // approval. If Admin rejects, RejectPayoutCommand reverses it.
        wallet.DebitForPayout(request.Amount);

        var payoutRequest = new PayoutRequest
        {
            VendorId = request.VendorId,
            Amount = request.Amount,
            Currency = wallet.Currency,
            BankNameSnapshot = vendor.BankName!,
            BankAccountNumberSnapshot = vendor.BankAccountNumber!,
            BankAccountNameSnapshot = vendor.BankAccountName ?? vendor.BusinessName
        };

        _context.PayoutRequests.Add(payoutRequest);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(payoutRequest.Id);
    }
}
