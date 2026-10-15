using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Exceptions;

namespace FashionCommerce.Domain.Entities.Payouts;

/// <summary>
/// One wallet per Vendor. This is the single source of truth for the
/// automated payout engine — Admin never manually calculates payouts;
/// every balance change here is driven by a CommissionTransaction or a
/// PayoutRequest being processed.
///
/// Balance meanings:
///  - PendingBalance: earnings from orders not yet eligible for payout
///    (e.g. still within the platform's return window).
///  - AvailableBalance: earnings cleared and withdrawable.
///  - WithdrawnBalance: lifetime total already paid out to the vendor's bank.
///  - RefundBalance: lifetime total deducted due to refunds after payout.
/// </summary>
public class VendorWallet : AuditableEntity
{
    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = default!;

    public decimal PendingBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal WithdrawnBalance { get; set; }
    public decimal RefundBalance { get; set; }

    public string Currency { get; set; } = "NGN";

    public void CreditPending(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        PendingBalance += amount;
    }

    /// <summary>Moves funds from Pending to Available once the return/dispute window has elapsed.</summary>
    public void ReleaseToAvailable(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (PendingBalance < amount)
            throw new DomainValidationException("Cannot release more than the pending balance.");

        PendingBalance -= amount;
        AvailableBalance += amount;
    }

    public void DebitForPayout(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (AvailableBalance < amount)
            throw new DomainValidationException("Insufficient available balance for payout.");

        AvailableBalance -= amount;
        WithdrawnBalance += amount;
    }

    /// <summary>
    /// Handles a refund after the vendor has already been paid out for that
    /// item: the amount is clawed back from AvailableBalance if possible,
    /// otherwise the wallet goes negative (offset against future earnings),
    /// per the platform's refund policy.
    /// </summary>
    public void ApplyPostPayoutRefundClawback(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        AvailableBalance -= amount; // intentionally allowed to go negative
        RefundBalance += amount;
    }

    public void ApplyPrePayoutRefund(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var fromPending = Math.Min(PendingBalance, amount);
        PendingBalance -= fromPending;

        var remainder = amount - fromPending;
        if (remainder > 0)
            AvailableBalance -= remainder; // may also go negative here

        RefundBalance += amount;
    }
}
