using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Domain.Entities.Support;

/// <summary>
/// App-wide support ticket, distinct from vendor Chat: this is for
/// platform-level issues — "contact the developer", bug reports, billing
/// questions — routed to Admin/SupportAgent staff rather than a vendor.
/// A BotConversation commonly escalates into one of these when the bot
/// can't resolve the issue itself.
/// </summary>
public class SupportTicket : AuditableEntity
{
    public string TicketNumber { get; set; } = default!;

    /// <summary>The reporting user's Customer/Vendor/Admin Id (interpretation depends on RequesterRole).</summary>
    public Guid RequesterId { get; set; }
    public UserRole RequesterRole { get; set; }

    public SupportTicketCategory Category { get; set; }
    public SupportTicketPriority Priority { get; set; } = SupportTicketPriority.Normal;
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;

    public string Subject { get; set; } = default!;
    public string Description { get; set; } = default!;

    /// <summary>App/device/browser context automatically attached, useful for bug reports.</summary>
    public string? ClientContextJson { get; set; }

    public Guid? AssignedAgentId { get; set; }
    public Guid? EscalatedFromBotConversationId { get; set; }

    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }

    public ICollection<SupportTicketMessage> Messages { get; set; } = new List<SupportTicketMessage>();
    public ICollection<SupportTicketAttachment> Attachments { get; set; } = new List<SupportTicketAttachment>();

    public void Resolve(string note)
    {
        Status = SupportTicketStatus.Resolved;
        ResolutionNote = note;
        ResolvedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        Status = SupportTicketStatus.Reopened;
        ResolvedAt = null;
    }
}

public class SupportTicketMessage : BaseEntity
{
    public Guid SupportTicketId { get; set; }
    public SupportTicket SupportTicket { get; set; } = default!;

    public Guid SenderId { get; set; }
    public UserRole SenderRole { get; set; }
    public string Content { get; set; } = default!;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public class SupportTicketAttachment : BaseEntity
{
    public Guid SupportTicketId { get; set; }
    public SupportTicket SupportTicket { get; set; } = default!;

    public string Url { get; set; } = default!;
    public string FileName { get; set; } = default!;
}
