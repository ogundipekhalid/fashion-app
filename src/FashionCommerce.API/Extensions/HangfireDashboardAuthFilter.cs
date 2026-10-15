using Hangfire.Dashboard;

namespace FashionCommerce.API.Extensions;

/// <summary>Restricts the Hangfire dashboard (/hangfire) to authenticated Admin/SuperAdmin users.</summary>
public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;

        return user.Identity?.IsAuthenticated == true &&
               (user.IsInRole("Admin") || user.IsInRole("SuperAdmin"));
    }
}
