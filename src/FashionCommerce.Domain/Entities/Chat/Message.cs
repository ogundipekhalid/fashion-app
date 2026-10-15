using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Domain.Entities.Chat;

public class Message : AuditableEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = default!;

    /// <summary>The Customer/Vendor/Admin Id of the sender (interpretation depends on SenderType).</summary>
    public Guid SenderId { get; set; }
    public MessageSenderType SenderType { get; set; }

    public MessageType Type { get; set; } = MessageType.Text;
    public string? Content { get; set; }
    public string? AttachmentUrl { get; set; }

    /// <summary>Set when Type is ProductReference/OrderReference, to deep-link within the message.</summary>
    public Guid? ReferencedEntityId { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>True if ChatModerationService caught this message sharing contact/account info or a suspicious digit pattern. Blocked messages are still stored (for the admin review trail) but never delivered to the other participant.</summary>
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }

    public void MarkAsRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }

    public void Block(string reason)
    {
        IsBlocked = true;
        BlockReason = reason;
    }
}
