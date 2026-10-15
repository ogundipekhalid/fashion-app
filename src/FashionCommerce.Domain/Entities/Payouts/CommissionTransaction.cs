using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Domain.Entities.Payouts;

/// <summary>
/// Immutable ledger entry recording exactly how one OrderItem's revenue
/// was split between platform and vendor. This is the audit trail behind
/// every number shown in VendorWallet — never mutate a settled row;
/// reversals get their own row (Status = Reversed) instead.
/// </summary>
public class CommissionTransaction : BaseEntity
{
    public Guid OrderItemId { get; set; }
    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = default!;

    public decimal GrossAmount { get; set; }
    public decimal CommissionRatePercent { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal VendorEarning { get; set; }

    public CommissionTransactionStatus Status { get; set; } = CommissionTransactionStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SettledAt { get; set; }
}

public class PayoutRequest : AuditableEntity
{
    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = default!;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public PayoutRequestStatus Status { get; set; } = PayoutRequestStatus.Requested;

    // Bank details snapshot at time of request, in case vendor changes bank info later.
    public string BankNameSnapshot { get; set; } = default!;
    public string BankAccountNumberSnapshot { get; set; } = default!;
    public string BankAccountNameSnapshot { get; set; } = default!;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public Guid? ProcessedByAdminId { get; set; }
    public string? FailureReason { get; set; }
    public string? ExternalTransferReference { get; set; }
}
