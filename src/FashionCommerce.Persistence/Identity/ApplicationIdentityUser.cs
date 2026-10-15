using Microsoft.AspNetCore.Identity;

namespace FashionCommerce.Persistence.Identity;

/// <summary>
/// The actual ASP.NET Core Identity user (auth-only: email, password hash,
/// lockout, 2FA). Deliberately thin — business/profile data (name, KYC,
/// business info) lives on Domain's Customer/Vendor/Admin, linked via
/// IdentityUserId. This keeps auth concerns and business concerns from
/// bleeding into each other.
/// </summary>
public class ApplicationIdentityUser : IdentityUser<Guid>
{
    /// <summary>Denormalized for convenience in a few Identity-only queries; source of truth is still Domain.</summary>
    public string? DisplayName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Enables "Inactive User Auto Logout after 30 minutes" — updated on every authenticated request.</summary>
    public DateTime? LastActivityAt { get; set; }
}

/// <summary>Roles table uses the framework default IdentityRole&lt;Guid&gt; — no custom fields needed.</summary>
public static class ApplicationRoles
{
    public const string Customer = "Customer";
    public const string Vendor = "Vendor";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
    public const string SupportAgent = "SupportAgent";
    public const string DeliveryAgent = "DeliveryAgent";

    public static readonly string[] All = { Customer, Vendor, Admin, SuperAdmin, SupportAgent, DeliveryAgent };
}
