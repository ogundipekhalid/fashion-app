using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FashionCommerce.Infrastructure.Jobs;

public static class HangfireRegistration
{
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found for Hangfire storage.");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString));

        services.AddHangfireServer();

        services.AddScoped<FundReleaseJob>();
        services.AddScoped<LowStockAlertJob>();

        return services;
    }

    /// <summary>
    /// Registers the recurring jobs. Called once at startup, after the app
    /// is built — RecurringJob.AddOrUpdate is idempotent, so this is safe
    /// to run on every app restart without creating duplicate schedules.
    /// </summary>
    public static void ScheduleRecurringJobs()
    {
        // Runs nightly: moves commission earnings from Pending to Available
        // once they're past the return window. See VendorWallet /
        // ReleaseEligibleFundsCommand for the actual business logic.
        RecurringJob.AddOrUpdate<FundReleaseJob>(
            "release-eligible-funds",
            job => job.RunAsync(CancellationToken.None),
            Cron.Daily(2)); // 02:00 UTC — off-peak

        // Runs every 6 hours: flags variants at/under their low-stock threshold.
        RecurringJob.AddOrUpdate<LowStockAlertJob>(
            "low-stock-alerts",
            job => job.RunAsync(CancellationToken.None),
            "0 */6 * * *");
    }
}
