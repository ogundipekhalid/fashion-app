using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Domain.Events;
using FashionCommerce.Domain.Exceptions;

namespace FashionCommerce.Domain.Entities.Refunds;

/// <summary>
/// Refund engine aggregate. Encodes the rule set from the spec:
///  - Before vendor payout: full amount refundable from the platform.
///  - After vendor payout: clawed back from the vendor's wallet (may go
///    negative and offset future earnings) — see VendorWallet.
///  - Platform commission may be fully refunded, partially refunded, or
///    retained, per PlatformCommissionRefundPolicy chosen when approving.
/// </summary>
public class RefundRequest : AuditableEntity
{
    public Guid OrderItemId { get; set; }
    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = default!;

    public RefundType Type { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }

    public string Reason { get; set; } = default!;
    public string? CustomerNote { get; set; }
    public string? VendorResponseNote { get; set; }
    public string? AdminDecisionNote { get; set; }

    /// <summary>Whether the platform commission portion is refunded to the customer too.</summary>
    public PlatformCommissionRefundPolicy CommissionPolicy { get; set; } = PlatformCommissionRefundPolicy.Retain;

    public RefundStatus Status { get; private set; } = RefundStatus.Requested;
    public RefundDecisionMaker? LastDecisionMaker { get; private set; }

    /// <summary>True if the item's vendor payout had already been processed when this refund was requested.</summary>
    public bool WasVendorAlreadyPaidOut { get; set; }

    public ICollection<RefundEvidence> Evidence { get; set; } = new List<RefundEvidence>();
    public ICollection<RefundAuditLog> AuditLogs { get; set; } = new List<RefundAuditLog>();

    public static RefundRequest Create(Guid orderItemId, Guid orderId, Guid customerId, Guid vendorId,
        RefundType type, decimal requestedAmount, string reason, bool vendorAlreadyPaidOut)
    {
        if (requestedAmount <= 0)
            throw new DomainValidationException("Refund amount must be greater than zero.");

        var request = new RefundRequest
        {
            OrderItemId = orderItemId,
            OrderId = orderId,
            CustomerId = customerId,
            VendorId = vendorId,
            Type = type,
            RequestedAmount = requestedAmount,
            Reason = reason,
            WasVendorAlreadyPaidOut = vendorAlreadyPaidOut
        };

        request.LogAudit(RefundDecisionMaker.System, "Refund request created.");
        return request;
    }

    public void SubmitVendorResponse(bool approved, string? note)
    {
        EnsureStatus(RefundStatus.AwaitingVendorResponse, RefundStatus.Requested, RefundStatus.UnderReview);

        VendorResponseNote = note;
        Status = approved ? RefundStatus.ApprovedByVendor : RefundStatus.RejectedByVendor;
        LastDecisionMaker = RefundDecisionMaker.Vendor;
        LogAudit(RefundDecisionMaker.Vendor, approved ? "Vendor approved refund." : "Vendor rejected refund.");
    }

    public void EscalateToAdmin(string reason)
    {
        Status = RefundStatus.EscalatedToAdmin;
        LogAudit(RefundDecisionMaker.System, $"Escalated to admin: {reason}");
    }

    public void AdminDecide(bool approved, decimal approvedAmount, string? note, PlatformCommissionRefundPolicy commissionPolicy)
    {
        if (approved && approvedAmount <= 0)
            throw new DomainValidationException("Approved amount must be greater than zero.");
        if (approved && approvedAmount > RequestedAmount)
            throw new DomainValidationException("Approved amount cannot exceed the requested amount.");

        AdminDecisionNote = note;
        CommissionPolicy = commissionPolicy;
        LastDecisionMaker = RefundDecisionMaker.Admin;

        if (approved)
        {
            ApprovedAmount = approvedAmount;
            Status = RefundStatus.ApprovedByAdmin;
        }
        else
        {
            Status = RefundStatus.RejectedByAdmin;
        }

        LogAudit(RefundDecisionMaker.Admin, approved ? $"Admin approved refund of {approvedAmount}." : "Admin rejected refund.");
    }

    public void MarkProcessing()
    {
        EnsureStatus(RefundStatus.Processing, RefundStatus.ApprovedByVendor, RefundStatus.ApprovedByAdmin);
        Status = RefundStatus.Processing;
        LogAudit(RefundDecisionMaker.System, "Refund processing started with payment provider.");
    }

    public void MarkRefunded(decimal actualAmount)
    {
        Status = RefundStatus.Refunded;
        ApprovedAmount ??= actualAmount;
        LogAudit(RefundDecisionMaker.System, $"Refund of {actualAmount} completed.");
        AddDomainEvent(new OrderItemRefundedEvent(OrderItemId, OrderId, VendorId, actualAmount));
    }

    public void Close(string? note = null)
    {
        Status = RefundStatus.Closed;
        LogAudit(RefundDecisionMaker.System, note ?? "Refund case closed.");
    }

    private void LogAudit(RefundDecisionMaker actor, string message) =>
        AuditLogs.Add(new RefundAuditLog
        {
            RefundRequestId = Id,
            Actor = actor,
            Message = message,
            Timestamp = DateTime.UtcNow
        });

    private void EnsureStatus(RefundStatus target, params RefundStatus[] validFrom)
    {
        if (!validFrom.Contains(Status))
            throw new RefundNotEligibleException($"Cannot move refund from '{Status}' to '{target}'.");
    }
}

public enum PlatformCommissionRefundPolicy
{
    Retain = 1,
    RefundFull = 2,
    RefundPartial = 3
}

public class RefundEvidence : BaseEntity
{
    public Guid RefundRequestId { get; set; }
    public RefundRequest RefundRequest { get; set; } = default!;

    public string Url { get; set; } = default!;
    public bool IsVideo { get; set; }
    public string UploadedByRole { get; set; } = default!; // "Customer" | "Vendor"
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class RefundAuditLog : BaseEntity
{
    public Guid RefundRequestId { get; set; }
    public RefundRequest RefundRequest { get; set; } = default!;

    public RefundDecisionMaker Actor { get; set; }
    public string Message { get; set; } = default!;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
