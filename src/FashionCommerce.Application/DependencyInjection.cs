using System.Reflection;
using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FashionCommerce.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR (with its pipeline behaviors, in execution order),
    /// FluentValidation validators, and AutoMapper — all auto-discovered
    /// from this assembly via reflection so new features register
    /// themselves without touching this file.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // Order matters: exceptions bubble up through Logging, Validation runs before the handler.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));

        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(_ => { }, assembly);

        services.AddScoped<IChatModerationService, ChatModerationService>();
        services.AddScoped<IBotReplyService, RuleBasedBotReplyService>();

        return services;
    }
}
