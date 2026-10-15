using FashionCommerce.Domain.Entities.Refunds;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Domain;

public class RefundRequestTests
{
    private static RefundRequest CreateRefund(bool vendorAlreadyPaidOut = false) => RefundRequest.Create(
        orderItemId: Guid.NewGuid(), orderId: Guid.NewGuid(), customerId: Guid.NewGuid(), vendorId: Guid.NewGuid(),
        type: RefundType.Full, requestedAmount: 100m, reason: "Item arrived damaged", vendorAlreadyPaidOut: vendorAlreadyPaidOut);

    [Fact]
    public void Create_starts_in_Requested_status_with_an_audit_entry()
    {
        var refund = CreateRefund();

        refund.Status.Should().Be(RefundStatus.Requested);
        refund.AuditLogs.Should().ContainSingle(a => a.Message.Contains("created"));
    }

    [Fact]
    public void Create_rejects_zero_or_negative_amount()
    {
        var act = () => RefundRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RefundType.Full, 0m, "reason", false);

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Vendor_approval_transitions_to_ApprovedByVendor()
    {
        var refund = CreateRefund();

        refund.SubmitVendorResponse(approved: true, note: "Confirmed damaged, approving.");

        refund.Status.Should().Be(RefundStatus.ApprovedByVendor);
        refund.LastDecisionMaker.Should().Be(RefundDecisionMaker.Vendor);
    }

    [Fact]
    public void Vendor_rejection_transitions_to_RejectedByVendor_and_can_then_be_escalated()
    {
        var refund = CreateRefund();

        refund.SubmitVendorResponse(approved: false, note: "Item was not damaged on our end.");
        refund.Status.Should().Be(RefundStatus.RejectedByVendor);

        refund.EscalateToAdmin("Vendor rejected the refund request.");
        refund.Status.Should().Be(RefundStatus.EscalatedToAdmin);
    }

    [Fact]
    public void AdminDecide_approving_requires_a_positive_amount_not_exceeding_the_requested_amount()
    {
        var refund = CreateRefund();
        refund.EscalateToAdmin("test");

        var act = () => refund.AdminDecide(approved: true, approvedAmount: 999m, note: null, PlatformCommissionRefundPolicy.Retain);

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void AdminDecide_approving_within_bounds_succeeds()
    {
        var refund = CreateRefund();
        refund.EscalateToAdmin("test");

        refund.AdminDecide(approved: true, approvedAmount: 80m, note: "Partial refund approved", PlatformCommissionRefundPolicy.RefundPartial);

        refund.Status.Should().Be(RefundStatus.ApprovedByAdmin);
        refund.ApprovedAmount.Should().Be(80m);
        refund.CommissionPolicy.Should().Be(PlatformCommissionRefundPolicy.RefundPartial);
    }

    [Fact]
    public void MarkProcessing_then_MarkRefunded_completes_the_case_and_raises_the_refunded_event()
    {
        var refund = CreateRefund();
        refund.SubmitVendorResponse(approved: true, note: null);

        refund.MarkProcessing();
        refund.Status.Should().Be(RefundStatus.Processing);

        refund.MarkRefunded(100m);

        refund.Status.Should().Be(RefundStatus.Refunded);
        refund.DomainEvents.Should().Contain(e => e.GetType().Name == "OrderItemRefundedEvent");
    }

    [Fact]
    public void MarkProcessing_cannot_run_from_Requested_status_without_an_approval_first()
    {
        var refund = CreateRefund();

        var act = () => refund.MarkProcessing();

        act.Should().Throw<RefundNotEligibleException>();
    }

    [Fact]
    public void WasVendorAlreadyPaidOut_is_preserved_from_creation_for_later_wallet_clawback_routing()
    {
        var refund = CreateRefund(vendorAlreadyPaidOut: true);

        refund.WasVendorAlreadyPaidOut.Should().BeTrue();
    }
}
