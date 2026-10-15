using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Domain.Entities.Support;

/// <summary>
/// A single conversation session with the platform's AI bot. One entity
/// serves multiple bot personas via <see cref="Channel"/>: the AI styling
/// assistant, the general customer-support bot, and the "get in touch with
/// the developer / report an issue" bot. Keeping them on one model lets us
/// share infrastructure (AI service client, escalation-to-ticket flow)
/// while still reporting on them separately by channel.
/// </summary>
public class BotConversation : AuditableEntity
{
    public Guid UserId { get; set; }
    public UserRole UserRole { get; set; }

    public BotChannel Channel { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    /// <summary>Set if the bot could not resolve the issue and handed off to a human via a SupportTicket.</summary>
    public Guid? EscalatedToSupportTicketId { get; set; }

    public ICollection<BotMessage> Messages { get; set; } = new List<BotMessage>();

    public void EscalateToTicket(Guid supportTicketId)
    {
        EscalatedToSupportTicketId = supportTicketId;
        EndedAt = DateTime.UtcNow;
    }

    public void End() => EndedAt ??= DateTime.UtcNow;
}

public class BotMessage : BaseEntity
{
    public Guid BotConversationId { get; set; }
    public BotConversation BotConversation { get; set; } = default!;

    public BotMessageSender Sender { get; set; }
    public string Content { get; set; } = default!;

    /// <summary>Detected user intent, e.g. "report_bug", "product_recommendation" — populated by the AI/NLU layer.</summary>
    public string? Intent { get; set; }
    public double? ConfidenceScore { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
