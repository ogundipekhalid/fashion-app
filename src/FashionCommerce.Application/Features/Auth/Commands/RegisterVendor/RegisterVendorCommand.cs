using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Entities.Payouts;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FashionCommerce.Application.Features.Auth.Commands.RegisterVendor;

public record RegisterVendorCommand : IRequest<Result<RegisterVendorResponse>>
{
    public string BusinessName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Password { get; init; } = default!;
    public string ContactPhone { get; init; } = default!;
    public string? BusinessDescription { get; init; }
}

public record RegisterVendorResponse(Guid VendorId, string Email, string VerificationStatus);

public class RegisterVendorCommandValidator : AbstractValidator<RegisterVendorCommand>
{
    public RegisterVendorCommandValidator()
    {
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.ContactPhone).NotEmpty().Matches(@"^\+?[0-9]{7,15}$");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
    }
}

public class RegisterVendorCommandHandler : IRequestHandler<RegisterVendorCommand, Result<RegisterVendorResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IEmailTemplateService _emailTemplateService;

    public RegisterVendorCommandHandler(
        IApplicationDbContext context,
        IIdentityService identityService,
        IEmailTemplateService emailTemplateService)
    {
        _context = context;
        _identityService = identityService;
        _emailTemplateService = emailTemplateService;
    }

    public async Task<Result<RegisterVendorResponse>> Handle(RegisterVendorCommand request, CancellationToken cancellationToken)
    {
        if (await _identityService.EmailExistsAsync(request.Email))
            return Result<RegisterVendorResponse>.Failure("An account with this email already exists.");

        var createUserResult = await _identityService.CreateUserAsync(request.Email, request.Password, UserRole.Vendor);
        if (!createUserResult.Succeeded)
            return Result<RegisterVendorResponse>.Failure(createUserResult.Errors);

        var identityUserId = createUserResult.Value;

        var vendor = new Vendor
        {
            IdentityUserId = identityUserId,
            BusinessName = request.BusinessName,
            BusinessDescription = request.BusinessDescription,
            ContactEmail = request.Email,
            ContactPhone = request.ContactPhone,
            VerificationStatus = VendorVerificationStatus.Pending,
            KycStatus = KycStatus.NotSubmitted
        };

        _context.Vendors.Add(vendor);

        // Every vendor gets a wallet the moment they register, at zero balance,
        // so the payout engine has something to credit as soon as their first sale settles.
        _context.VendorWallets.Add(new VendorWallet
        {
            VendorId = vendor.Id,
            Vendor = vendor
        });

        await _context.SaveChangesAsync(cancellationToken);

        var token = await _identityService.GenerateEmailConfirmationTokenAsync(identityUserId);
        var verificationLink = $"https://app.fashioncommerce.dev/verify-email?userId={identityUserId}&token={Uri.EscapeDataString(token)}";
        await _emailTemplateService.SendEmailVerificationAsync(request.Email, vendor.BusinessName, verificationLink, cancellationToken);

        return Result<RegisterVendorResponse>.Success(
            new RegisterVendorResponse(vendor.Id, vendor.ContactEmail, vendor.VerificationStatus.ToString()));
    }
}
