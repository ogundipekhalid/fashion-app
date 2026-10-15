using System.Net;
using System.Net.Mail;
using FashionCommerce.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace FashionCommerce.Infrastructure.Email;

public class SmtpSettings
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = default!;
    public int SmtpPort { get; set; } = 587;
    public string FromAddress { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public bool EnableSsl { get; set; } = true;
}

/// <summary>
/// Plain SMTP sender (works with Mailtrap/SendGrid/etc's SMTP relay in dev,
/// or any production SMTP provider). Swappable for a provider-specific SDK
/// later without touching Application/Domain — it only knows IEmailService.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _settings;

    public SmtpEmailService(IOptions<SmtpSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromAddress, "FashionCommerce"),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            Credentials = new NetworkCredential(_settings.Username, _settings.Password),
            EnableSsl = _settings.EnableSsl
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
