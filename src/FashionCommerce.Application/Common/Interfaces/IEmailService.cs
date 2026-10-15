namespace FashionCommerce.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thin wrappers around named transactional email templates so command
/// handlers don't build HTML themselves — they just say "send this kind
/// of email with these parameters". Implemented in Infrastructure.
/// </summary>
public interface IEmailTemplateService
{
    Task SendEmailVerificationAsync(string toEmail, string recipientName, string verificationLink, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string toEmail, string recipientName, string resetLink, CancellationToken cancellationToken = default);
    Task SendWelcomeEmailAsync(string toEmail, string recipientName, CancellationToken cancellationToken = default);
}
