using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FashionCommerce.Application.Features.Auth.Commands.RegisterCustomer;

public record RegisterCustomerCommand : IRequest<Result<RegisterCustomerResponse>>
{
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Password { get; init; } = default!;
    public string? PhoneNumber { get; init; }
}

public record RegisterCustomerResponse(Guid CustomerId, string Email);

public class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);

        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);

        // Strong password policy: min 8 chars, at least one upper, one lower, one digit, one special char.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[0-9]{7,15}$").When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Phone number is not valid.");
    }
}

public class RegisterCustomerCommandHandler : IRequestHandler<RegisterCustomerCommand, Result<RegisterCustomerResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IEmailTemplateService _emailTemplateService;

    public RegisterCustomerCommandHandler(
        IApplicationDbContext context,
        IIdentityService identityService,
        IEmailTemplateService emailTemplateService)
    {
        _context = context;
        _identityService = identityService;
        _emailTemplateService = emailTemplateService;
    }

    public async Task<Result<RegisterCustomerResponse>> Handle(RegisterCustomerCommand request, CancellationToken cancellationToken)
    {
        if (await _identityService.EmailExistsAsync(request.Email))
            return Result<RegisterCustomerResponse>.Failure("An account with this email already exists.");

        var createUserResult = await _identityService.CreateUserAsync(request.Email, request.Password, UserRole.Customer);
        if (!createUserResult.Succeeded)
            return Result<RegisterCustomerResponse>.Failure(createUserResult.Errors);

        var identityUserId = createUserResult.Value;

        var customer = new Customer
        {
            IdentityUserId = identityUserId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            IsEmailVerified = false
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        var token = await _identityService.GenerateEmailConfirmationTokenAsync(identityUserId);
        var verificationLink = $"https://app.fashioncommerce.dev/verify-email?userId={identityUserId}&token={Uri.EscapeDataString(token)}";
        await _emailTemplateService.SendEmailVerificationAsync(request.Email, customer.FullName, verificationLink, cancellationToken);

        return Result<RegisterCustomerResponse>.Success(new RegisterCustomerResponse(customer.Id, customer.Email));
    }
}
