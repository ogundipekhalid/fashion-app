using FashionCommerce.Domain.Entities.Payouts;
using FashionCommerce.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Domain;

public class VendorWalletTests
{
    private static VendorWallet CreateWallet() => new() { VendorId = Guid.NewGuid() };

    [Fact]
    public void CreditPending_increases_pending_balance()
    {
        var wallet = CreateWallet();

        wallet.CreditPending(500m);

        wallet.PendingBalance.Should().Be(500m);
    }

    [Fact]
    public void ReleaseToAvailable_moves_funds_from_pending_to_available()
    {
        var wallet = CreateWallet();
        wallet.CreditPending(500m);

        wallet.ReleaseToAvailable(300m);

        wallet.PendingBalance.Should().Be(200m);
        wallet.AvailableBalance.Should().Be(300m);
    }

    [Fact]
    public void ReleaseToAvailable_throws_when_amount_exceeds_pending_balance()
    {
        var wallet = CreateWallet();
        wallet.CreditPending(100m);

        var act = () => wallet.ReleaseToAvailable(200m);

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void DebitForPayout_moves_funds_from_available_to_withdrawn()
    {
        var wallet = CreateWallet();
        wallet.CreditPending(500m);
        wallet.ReleaseToAvailable(500m);

        wallet.DebitForPayout(400m);

        wallet.AvailableBalance.Should().Be(100m);
        wallet.WithdrawnBalance.Should().Be(400m);
    }

    [Fact]
    public void DebitForPayout_throws_when_amount_exceeds_available_balance()
    {
        var wallet = CreateWallet();
        wallet.CreditPending(100m);
        wallet.ReleaseToAvailable(100m);

        var act = () => wallet.DebitForPayout(150m);

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void ApplyPrePayoutRefund_deducts_from_pending_first_before_touching_available()
    {
        var wallet = CreateWallet();
        wallet.CreditPending(1000m); // simulate two order items credited
        wallet.ReleaseToAvailable(400m); // one item's earnings already released

        // Refunding an item whose earnings were NOT yet released (600 still pending)
        wallet.ApplyPrePayoutRefund(300m);

        wallet.PendingBalance.Should().Be(300m); // 600 - 300
        wallet.AvailableBalance.Should().Be(400m); // untouched
        wallet.RefundBalance.Should().Be(300m);
    }

    [Fact]
    public void ApplyPrePayoutRefund_spills_into_available_when_pending_is_insufficient()
    {
        var wallet = CreateWallet();
        wallet.CreditPending(200m);
        wallet.ReleaseToAvailable(200m); // all released, pending now 0

        wallet.CreditPending(50m); // a little more pending from a new order

        wallet.ApplyPrePayoutRefund(150m); // more than the 50 pending

        wallet.PendingBalance.Should().Be(0m);
        wallet.AvailableBalance.Should().Be(100m); // 200 - 100 (the remainder of the refund)
        wallet.RefundBalance.Should().Be(150m);
    }

    [Fact]
    public void ApplyPostPayoutRefundClawback_can_drive_available_balance_negative()
    {
        // This is the core "vendor already paid out" rule from the spec:
        // the clawback still applies even if it overdraws the vendor,
        // offsetting future earnings rather than being blocked.
        var wallet = CreateWallet();
        wallet.CreditPending(100m);
        wallet.ReleaseToAvailable(100m);
        wallet.DebitForPayout(100m); // vendor has already been paid, Available is now 0

        wallet.ApplyPostPayoutRefundClawback(75m);

        wallet.AvailableBalance.Should().Be(-75m);
        wallet.RefundBalance.Should().Be(75m);
    }

    [Fact]
    public void Negative_or_zero_amounts_are_rejected_across_all_operations()
    {
        var wallet = CreateWallet();

        FluentActions.Invoking(() => wallet.CreditPending(0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => wallet.CreditPending(-10)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => wallet.ApplyPostPayoutRefundClawback(-1)).Should().Throw<ArgumentOutOfRangeException>();
    }
}
