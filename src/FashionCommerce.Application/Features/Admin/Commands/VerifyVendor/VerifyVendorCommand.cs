using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Entities.Notifications;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Admin.Commands.VerifyVendor;

public record VerifyVendorCommand : IRequest<Result>, IAuditableRequest
{
    public Guid VendorId { get; init; }
    public bool Approved { get; init; }
    public string? RejectionReason { get; init; }

    public string Action => "Vendor.VerificationDecided";
    public string EntityName => "Vendor";
}

public class VerifyVendorCommandValidator : AbstractValidator<VerifyVendorCommand>
{
    public VerifyVendorCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.RejectionReason).NotEmpty().When(x => !x.Approved).WithMessage("A reason is required when rejecting a vendor.");
    }
}

public class VerifyVendorCommandHandler : IRequestHandler<VerifyVendorCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public VerifyVendorCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(VerifyVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == request.VendorId, cancellationToken);
        if (vendor is null)
            throw new NotFoundException(nameof(Vendor), request.VendorId);

        vendor.VerificationStatus = request.Approved ? VendorVerificationStatus.Verified : VendorVerificationStatus.Rejected;
        vendor.RejectionReason = request.Approved ? null : request.RejectionReason;

        _context.Notifications.Add(new Notification
        {
            UserId = vendor.Id,
            UserRole = UserRole.Vendor,
            Type = request.Approved ? NotificationType.AccountSecurity : NotificationType.AccountSecurity,
            Title = request.Approved ? "Your vendor account is verified!" : "Vendor verification was not approved",
            Body = request.Approved
                ? "Congratulations — you're verified and can start listing products."
                : $"Your verification was not approved: {request.RejectionReason}",
            SentAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
