using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Refunds;
using FashionCommerce.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Refunds.Commands.AdminDecideRefund;

public record AdminDecideRefundCommand : IRequest<Result>, IAuditableRequest
{
    public Guid RefundRequestId { get; init; }
    public bool Approved { get; init; }
    public decimal ApprovedAmount { get; init; }
    public string? Note { get; init; }

    /// <summary>"Retain" | "RefundFull" | "RefundPartial" — how much of the platform's commission on this item is also refunded.</summary>
    public string CommissionPolicy { get; init; } = "Retain";

    public string Action => "RefundRequest.AdminDecided";
    public string EntityName => "RefundRequest";
}

public class AdminDecideRefundCommandValidator : AbstractValidator<AdminDecideRefundCommand>
{
    public AdminDecideRefundCommandValidator()
    {
        RuleFor(x => x.RefundRequestId).NotEmpty();
        RuleFor(x => x.ApprovedAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CommissionPolicy).Must(p => Enum.TryParse<PlatformCommissionRefundPolicy>(p, out _)).WithMessage("Invalid commission policy.");
    }
}

public class AdminDecideRefundCommandHandler : IRequestHandler<AdminDecideRefundCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public AdminDecideRefundCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(AdminDecideRefundCommand request, CancellationToken cancellationToken)
    {
        var refund = await _context.RefundRequests.FirstOrDefaultAsync(r => r.Id == request.RefundRequestId, cancellationToken);
        if (refund is null)
            throw new NotFoundException(nameof(RefundRequest), request.RefundRequestId);

        try
        {
            refund.AdminDecide(request.Approved, request.ApprovedAmount, request.Note, Enum.Parse<PlatformCommissionRefundPolicy>(request.CommissionPolicy));
        }
        catch (DomainValidationException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
