using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Orders;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Domain.Entities.Payments;

public class Payment : AuditableEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public PaymentProvider Provider { get; set; }

    /// <summary>Provider-issued reference (Paystack ref, Flutterwave tx_ref, Stripe PaymentIntent id).</summary>
    public string ProviderReference { get; set; } = default!;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime? InitializedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public string? FailureReason { get; set; }

    /// <summary>Raw provider response payload, kept for audit/dispute purposes.</summary>
    public string? RawResponseJson { get; set; }

    public ICollection<PaymentWebhookLog> WebhookLogs { get; set; } = new List<PaymentWebhookLog>();
}

/// <summary>
/// Every inbound webhook call is logged before processing, with its
/// signature verification result, so we have a full audit trail and can
/// safely make webhook handling idempotent (dedupe by ProviderEventId).
/// </summary>
public class PaymentWebhookLog : BaseEntity
{
    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public PaymentProvider Provider { get; set; }
    public string ProviderEventId { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public bool SignatureValid { get; set; }
    public string PayloadJson { get; set; } = default!;
    public bool Processed { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
