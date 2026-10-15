using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Payouts;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Domain.Entities.Identity;

/// <summary>
/// A marketplace seller. Holds business/KYC info and the commission rate
/// used by the payout engine. <see cref="CommissionRatePercent"/> defaults
/// to the platform rate but can be overridden per-vendor (e.g. negotiated
/// deals), which is why it lives on Vendor rather than being a single
/// global constant.
/// </summary>
public class Vendor : AuditableEntity
{
    public Guid IdentityUserId { get; set; }

    public string BusinessName { get; set; } = default!;
    public string? BusinessDescription { get; set; }
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string ContactEmail { get; set; } = default!;
    public string? ContactPhone { get; set; }

    public VendorVerificationStatus VerificationStatus { get; set; } = VendorVerificationStatus.Pending;
    public KycStatus KycStatus { get; set; } = KycStatus.NotSubmitted;
    public string? KycDocumentUrl { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Platform commission rate applied to this vendor's sales, e.g. 20.00 for 20%.</summary>
    public decimal CommissionRatePercent { get; set; } = 20.00m;

    // Bank/payout details (encrypted at rest by Infrastructure, stored here as opaque references)
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountName { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public VendorWallet? Wallet { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<PayoutRequest> PayoutRequests { get; set; } = new List<PayoutRequest>();
    public ICollection<FavoriteVendor> FavoritedByCustomers { get; set; } = new List<FavoriteVendor>();
}
