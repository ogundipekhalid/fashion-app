using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Auth.Commands.VerifyEmail;

public record VerifyEmailCommand : IRequest<Result>
{
    public Guid IdentityUserId { get; init; }
    public string Token { get; init; } = default!;
}

public class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.IdentityUserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty();
    }
}

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, Result>
{
    private readonly IIdentityService _identityService;
    private readonly IApplicationDbContext _context;
    private readonly IEmailTemplateService _emailTemplateService;

    public VerifyEmailCommandHandler(
        IIdentityService identityService,
        IApplicationDbContext context,
        IEmailTemplateService emailTemplateService)
    {
        _identityService = identityService;
        _context = context;
        _emailTemplateService = emailTemplateService;
    }

    public async Task<Result> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var confirmResult = await _identityService.ConfirmEmailAsync(request.IdentityUserId, request.Token);
        if (!confirmResult.Succeeded)
            return Result.Failure(confirmResult.Errors);

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.IdentityUserId == request.IdentityUserId, cancellationToken);

        if (customer is not null)
        {
            customer.IsEmailVerified = true;
            await _context.SaveChangesAsync(cancellationToken);
            await _emailTemplateService.SendWelcomeEmailAsync(customer.Email, customer.FullName, cancellationToken);
            return Result.Success();
        }

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.IdentityUserId == request.IdentityUserId, cancellationToken);

        if (vendor is not null)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _emailTemplateService.SendWelcomeEmailAsync(vendor.ContactEmail, vendor.BusinessName, cancellationToken);
            return Result.Success();
        }

        return Result.Success();
    }
}
