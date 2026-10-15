using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Infrastructure.Authentication;
using FashionCommerce.Infrastructure.AI;
using FashionCommerce.Infrastructure.Common;
using FashionCommerce.Infrastructure.Email;
using FashionCommerce.Infrastructure.Payments;
using FashionCommerce.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FashionCommerce.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers every Application-layer interface's concrete
    /// implementation: Identity/JWT (Authentication requirement), Email
    /// (Email Service requirement), CurrentUser/DateTime (cross-cutting).
    /// Payments/Storage/AI/SignalR/Hangfire registrations are added here as
    /// each of those modules gets built.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));

        services.AddHttpContextAccessor();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IPresenceTracker, InMemoryPresenceTracker>();
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();

        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();

        // Payments — all three named in the spec are now implemented (Paystack,
        // Flutterwave, Stripe). PaymentGatewayFactory resolves by PaymentProvider
        // at checkout time from whichever gateways are registered here.
        services.Configure<PaystackSettings>(configuration.GetSection(PaystackSettings.SectionName));
        services.AddHttpClient<PaystackPaymentGateway>();
        services.AddScoped<IPaymentGateway>(sp => sp.GetRequiredService<PaystackPaymentGateway>());

        services.Configure<FlutterwaveSettings>(configuration.GetSection(FlutterwaveSettings.SectionName));
        services.AddHttpClient<FlutterwavePaymentGateway>();
        services.AddScoped<IPaymentGateway>(sp => sp.GetRequiredService<FlutterwavePaymentGateway>());

        services.Configure<StripeSettings>(configuration.GetSection(StripeSettings.SectionName));
        services.AddHttpClient<StripePaymentGateway>();
        services.AddScoped<IPaymentGateway>(sp => sp.GetRequiredService<StripePaymentGateway>());

        services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();

        // Storage — provider chosen by "Storage:Provider" config ("Cloudinary" | "AzureBlob"),
        // defaulting to Cloudinary. Both implement the same IFileStorageService,
        // so this is the only place that needs to know which is active.
        services.Configure<CloudinarySettings>(configuration.GetSection(CloudinarySettings.SectionName));
        services.Configure<AzureBlobStorageSettings>(configuration.GetSection(AzureBlobStorageSettings.SectionName));

        var storageProvider = configuration["Storage:Provider"] ?? "Cloudinary";
        if (string.Equals(storageProvider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IFileStorageService, AzureBlobFileStorageService>();
        else
            services.AddScoped<IFileStorageService, CloudinaryFileStorageService>();

        // AI — Gemini's free tier makes it the right default for a launch-stage
        // app. Swapping providers later means writing one new IAiService
        // implementation, not touching any of its callers.
        services.Configure<GeminiSettings>(configuration.GetSection(GeminiSettings.SectionName));
        services.AddHttpClient<IAiService, GeminiAiService>();

        services.AddJwtAuthentication(configuration);

        return services;
    }
}
