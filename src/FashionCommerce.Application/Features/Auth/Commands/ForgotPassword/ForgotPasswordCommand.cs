using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace FashionCommerce.Application.Features.Auth.Commands.ForgotPassword;

public record ForgotPasswordCommand : IRequest<Result>
{
    public string Email { get; init; } = default!;
}

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly IIdentityService _identityService;
    private readonly IEmailTemplateService _emailTemplateService;

    public ForgotPasswordCommandHandler(IIdentityService identityService, IEmailTemplateService emailTemplateService)
    {
        _identityService = identityService;
        _emailTemplateService = emailTemplateService;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var identityUserId = await _identityService.FindIdentityUserIdByEmailAsync(request.Email);

        // Deliberately always return Success — revealing "email not found" here
        // lets an attacker enumerate registered accounts.
        if (identityUserId is null)
            return Result.Success();

        var token = await _identityService.GeneratePasswordResetTokenAsync(identityUserId.Value);
        var resetLink = $"https://app.fashioncommerce.dev/reset-password?userId={identityUserId}&token={Uri.EscapeDataString(token)}";

        await _emailTemplateService.SendPasswordResetAsync(request.Email, request.Email, resetLink, cancellationToken);

        return Result.Success();
    }
}
