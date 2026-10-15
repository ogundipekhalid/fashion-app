using FashionCommerce.Application.Common.Interfaces;

namespace FashionCommerce.Infrastructure.Email;

/// <summary>
/// Simple inline HTML templates for now. Swap the body-building for a real
/// templating engine (Razor/Scriban) or the payment/marketing provider's
/// hosted templates later — Application handlers only ever call these
/// named methods, so the swap is invisible to them.
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private readonly IEmailService _emailService;

    public EmailTemplateService(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public Task SendEmailVerificationAsync(string toEmail, string recipientName, string verificationLink, CancellationToken cancellationToken = default)
    {
        var body = $"""
            <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto;">
                <h2>Welcome to FashionCommerce, {recipientName}!</h2>
                <p>Please confirm your email address to activate your account.</p>
                <p><a href="{verificationLink}" style="background:#111;color:#fff;padding:12px 24px;text-decoration:none;border-radius:6px;">Verify Email</a></p>
                <p>If you didn't create this account, you can safely ignore this email.</p>
            </div>
            """;

        return _emailService.SendAsync(toEmail, "Verify your FashionCommerce email", body, cancellationToken);
    }

    public Task SendPasswordResetAsync(string toEmail, string recipientName, string resetLink, CancellationToken cancellationToken = default)
    {
        var body = $"""
            <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto;">
                <h2>Reset your password</h2>
                <p>Hi {recipientName}, we received a request to reset your password.</p>
                <p><a href="{resetLink}" style="background:#111;color:#fff;padding:12px 24px;text-decoration:none;border-radius:6px;">Reset Password</a></p>
                <p>This link will expire shortly. If you didn't request this, you can safely ignore this email.</p>
            </div>
            """;

        return _emailService.SendAsync(toEmail, "Reset your FashionCommerce password", body, cancellationToken);
    }

    public Task SendWelcomeEmailAsync(string toEmail, string recipientName, CancellationToken cancellationToken = default)
    {
        var body = $"""
            <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto;">
                <h2>You're all set, {recipientName}!</h2>
                <p>Your email is verified and your FashionCommerce account is ready to go.</p>
            </div>
            """;

        return _emailService.SendAsync(toEmail, "Welcome to FashionCommerce", body, cancellationToken);
    }
}
