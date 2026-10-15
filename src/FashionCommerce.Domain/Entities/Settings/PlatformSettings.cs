using FashionCommerce.Domain.Common;

namespace FashionCommerce.Domain.Entities.Settings;

/// <summary>
/// Singleton-row configuration table — exactly one row should ever exist.
/// Enforced at the Application layer (GetOrCreate-on-first-access pattern
/// in GetPlatformSettingsQuery), not the database, since EF Core has no
/// clean built-in way to constrain "at most one row" beyond a fixed
/// well-known Id, which is what <see cref="SingletonId"/> is for.
/// </summary>
public class PlatformSettings : AuditableEntity
{
    /// <summary>Fixed, well-known Id — every read/write targets this exact row, so there's never ambiguity about "which settings row".</summary>
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>Default commission rate applied to newly-registered vendors (existing vendors keep their own Vendor.CommissionRatePercent — this only affects new signups).</summary>
    public decimal DefaultVendorCommissionRatePercent { get; set; } = 20.00m;

    public decimal DefaultTaxRatePercent { get; set; } = 0m;
    public decimal DefaultShippingFee { get; set; } = 0m;

    /// <summary>Days after delivery before a vendor's commission earnings release from Pending to Available — feeds ReleaseEligibleFundsCommand's default window.</summary>
    public int PayoutHoldDays { get; set; } = 7;

    public string SupportEmail { get; set; } = "support@fashioncommerce.dev";
    public string PlatformName { get; set; } = "FashionCommerce";

    /// <summary>When true, non-Admin API requests could be rejected by a maintenance-mode check (not yet wired into middleware — the flag exists and is settable, the enforcement isn't built).</summary>
    public bool MaintenanceMode { get; set; }

    /// <summary>
    /// Creates the row with the fixed SingletonId. Exists because BaseEntity.Id
    /// has a protected setter (by design, so nothing outside Domain can assign
    /// an arbitrary Id) — this is the one sanctioned way to get a
    /// PlatformSettings instance whose Id is the well-known singleton value
    /// rather than a random new Guid.
    /// </summary>
    public static PlatformSettings CreateDefault()
    {
        var settings = new PlatformSettings();
        settings.SetSingletonId();
        return settings;
    }

    private void SetSingletonId() => Id = SingletonId; // allowed: protected setter accessed from within this same declaring type
}
