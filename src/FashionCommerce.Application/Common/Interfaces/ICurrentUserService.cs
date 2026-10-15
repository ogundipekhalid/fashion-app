using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Application.Common.Interfaces;

/// <summary>
/// Reads the authenticated user's identity from the current HTTP context
/// (JWT claims). Implemented in Infrastructure so Application handlers
/// never touch HttpContext directly, keeping them host-agnostic (works
/// the same in an API controller, a Hangfire background job, or a test).
/// </summary>
public interface ICurrentUserService
{
    Guid? IdentityUserId { get; }

    /// <summary>The Customer/Vendor/Admin.Id (not the IdentityUserId) matching the current role — resolved once per request.</summary>
    Guid? ProfileId { get; }

    UserRole? Role { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}

/// <summary>Thin wrapper over DateTime.UtcNow so handlers stay testable (inject a fake clock in unit tests).</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
