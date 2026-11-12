using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Auth.Commands.ResendVerificationEmail;

public record ResendVerificationEmailCommand : IRequest<Result>
{
    public string Email { get; init; } = default!;
}

public class ResendVerificationEmailCommandValidator : AbstractValidator<ResendVerificationEmailCommand>
{
    public ResendVerificationEmailCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public class ResendVerificationEmailCommandHandler : IRequestHandler<ResendVerificationEmailCommand, Result>
{
    private readonly IIdentityService _identityService;
    private readonly IApplicationDbContext _context;
    private readonly IEmailTemplateService _emailTemplateService;

    public ResendVerificationEmailCommandHandler(
        IIdentityService identityService,
        IApplicationDbContext context,
        IEmailTemplateService emailTemplateService)
    {
        _identityService = identityService;
        _context = context;
        _emailTemplateService = emailTemplateService;
    }

    public async Task<Result> Handle(ResendVerificationEmailCommand request, CancellationToken cancellationToken)
    {
        var identityUserId = await _identityService.FindIdentityUserIdByEmailAsync(request.Email.Trim());
        if (identityUserId is null || await _identityService.IsEmailConfirmedAsync(identityUserId.Value))
            return Result.Success();

        var customer = await _context.Customers.FirstOrDefaultAsync(
            c => c.IdentityUserId == identityUserId.Value, cancellationToken);
        var vendor = customer is null
            ? await _context.Vendors.FirstOrDefaultAsync(v => v.IdentityUserId == identityUserId.Value, cancellationToken)
            : null;

        var recipientName = customer?.FullName ?? vendor?.BusinessName ?? request.Email.Trim().Split('@')[0];
        var token = await _identityService.GenerateEmailConfirmationTokenAsync(identityUserId.Value);
        var verificationLink = $"https://app.fashioncommerce.dev/verify-email?userId={identityUserId.Value}&token={Uri.EscapeDataString(token)}";

        await _emailTemplateService.SendEmailVerificationAsync(request.Email.Trim(), recipientName, verificationLink, cancellationToken);
        return Result.Success();
    }
}
