using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Domain.Exceptions;

namespace FashionCommerce.Domain.Entities.Chat;

/// <summary>
/// A Customer&lt;-&gt;Vendor conversation thread. Deliberately ALWAYS scoped to
/// a Product — every conversation exists to discuss, complain about, or ask
/// about a specific item, never as a free-floating DM. <see cref="Type"/>
/// narrows the reason further (general product question, order issue,
/// refund case) and OrderId/RefundRequestId add extra context when relevant.
///
/// Two distinct moderation paths exist:
///  - Report(): a user reports the conversation (human-flagged).
///  - Flag(): the system auto-flags it after detecting suspicious content
///    (e.g. phone/account numbers), via ChatModerationService in the
///    Application layer. A flagged conversation is auto-Blocked, meaning no
///    further messages can be sent until Admin reviews and reopens it.
/// </summary>
public class Conversation : AuditableEntity
{
    public ConversationType Type { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.Open;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = default!;

    /// <summary>Every conversation is anchored to the product being discussed — required, not optional.</summary>
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    /// <summary>Set when Type is OrderEnquiry or RefundEnquiry.</summary>
    public Guid? OrderId { get; set; }

    /// <summary>Set when Type is RefundEnquiry and tied to a specific refund case.</summary>
    public Guid? RefundRequestId { get; set; }

    public string? Subject { get; set; }
    public DateTime? LastMessageAt { get; set; }

    // User-initiated report.
    public bool IsReported { get; set; }
    public string? ReportReason { get; set; }
    public Guid? ReportedBy { get; set; }

    // System-initiated auto-flag (content moderation).
    public bool IsAutoFlagged { get; set; }
    public string? AutoFlagReason { get; set; }
    public DateTime? AutoFlaggedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public static Conversation Start(Guid customerId, Guid vendorId, Guid productId, ConversationType type,
        Guid? orderId = null, Guid? refundRequestId = null, string? subject = null)
    {
        if (type is ConversationType.OrderEnquiry && orderId is null)
            throw new DomainValidationException("An order enquiry must reference an order.");

        if (type is ConversationType.RefundEnquiry && refundRequestId is null)
            throw new DomainValidationException("A refund enquiry must reference a refund request.");

        return new Conversation
        {
            CustomerId = customerId,
            VendorId = vendorId,
            ProductId = productId,
            Type = type,
            OrderId = orderId,
            RefundRequestId = refundRequestId,
            Subject = subject
        };
    }

    public bool CanAcceptMessages => Status is not (ConversationStatus.Blocked or ConversationStatus.Closed);

    public void Close() => Status = ConversationStatus.Closed;

    public void Report(Guid reportedBy, string reason)
    {
        IsReported = true;
        ReportedBy = reportedBy;
        ReportReason = reason;
        Status = ConversationStatus.Reported;
    }

    /// <summary>Called by ChatModerationService when a message is caught sharing contact/account info or spamming digit sequences. Immediately blocks further messages pending Admin review.</summary>
    public void FlagAndBlock(string reason)
    {
        IsAutoFlagged = true;
        AutoFlagReason = reason;
        AutoFlaggedAt = DateTime.UtcNow;
        Status = ConversationStatus.Blocked;
    }

    /// <summary>Admin clears the flag after review, allowing the conversation to resume.</summary>
    public void ClearFlagAndReopen()
    {
        IsAutoFlagged = false;
        Status = ConversationStatus.Open;
    }
}
