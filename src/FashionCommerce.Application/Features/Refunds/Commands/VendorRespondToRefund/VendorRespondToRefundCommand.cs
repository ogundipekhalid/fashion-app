using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Refunds;
using FashionCommerce.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Refunds.Commands.VendorRespondToRefund;

public record VendorRespondToRefundCommand : IRequest<Result>, IAuditableRequest
{
    public Guid RefundRequestId { get; init; }
    public Guid RequestingVendorId { get; init; }
    public bool Approved { get; init; }
    public string? Note { get; init; }

    public string Action => "RefundRequest.VendorResponded";
    public string EntityName => "RefundRequest";
}

public class VendorRespondToRefundCommandValidator : AbstractValidator<VendorRespondToRefundCommand>
{
    public VendorRespondToRefundCommandValidator()
    {
        RuleFor(x => x.RefundRequestId).NotEmpty();
    }
}

public class VendorRespondToRefundCommandHandler : IRequestHandler<VendorRespondToRefundCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public VendorRespondToRefundCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(VendorRespondToRefundCommand request, CancellationToken cancellationToken)
    {
        var refund = await _context.RefundRequests.FirstOrDefaultAsync(r => r.Id == request.RefundRequestId, cancellationToken);
        if (refund is null)
            throw new NotFoundException(nameof(RefundRequest), request.RefundRequestId);

        if (refund.VendorId != request.RequestingVendorId)
            throw new ForbiddenAccessException("This refund request is not associated with your store.");

        try
        {
            refund.SubmitVendorResponse(request.Approved, request.Note);
        }
        catch (RefundNotEligibleException ex)
        {
            return Result.Failure(ex.Message);
        }

        // A vendor rejection doesn't end the case — it escalates to Admin
        // arbitration automatically, per the spec's "Admin arbitration" requirement.
        if (!request.Approved)
            refund.EscalateToAdmin("Vendor rejected the refund request.");

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
