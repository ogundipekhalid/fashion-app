using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Admin.Commands.ToggleVendorActive;

/// <summary>
/// Suspending a vendor (IsActive = false) doesn't delete or hide their
/// existing products retroactively — that's a deliberate choice: customers
/// who already ordered from them still need order history/support to work.
/// New product creation and visibility in search should be gated on
/// IsActive by the Catalog module's query handlers going forward.
/// </summary>
public record ToggleVendorActiveCommand : IRequest<Result>, IAuditableRequest
{
    public Guid VendorId { get; init; }
    public bool IsActive { get; init; }
    public string? Reason { get; init; }

    public string Action => "Vendor.ActiveStatusToggled";
    public string EntityName => "Vendor";
}

public class ToggleVendorActiveCommandValidator : AbstractValidator<ToggleVendorActiveCommand>
{
    public ToggleVendorActiveCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().When(x => !x.IsActive).WithMessage("A reason is required when suspending a vendor.");
    }
}

public class ToggleVendorActiveCommandHandler : IRequestHandler<ToggleVendorActiveCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public ToggleVendorActiveCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(ToggleVendorActiveCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == request.VendorId, cancellationToken);
        if (vendor is null)
            throw new NotFoundException(nameof(Vendor), request.VendorId);

        vendor.IsActive = request.IsActive;
        if (!request.IsActive)
            vendor.VerificationStatus = Domain.Enums.VendorVerificationStatus.Suspended;
        else if (vendor.VerificationStatus == Domain.Enums.VendorVerificationStatus.Suspended)
            vendor.VerificationStatus = Domain.Enums.VendorVerificationStatus.Verified;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
