namespace FashionCommerce.Application.Common.Interfaces;

public record BotReply(string Content, string? DetectedIntent, bool ShouldEscalateToTicket, double ConfidenceScore);

/// <summary>
/// Produces the bot's reply to a user message. Currently rule-based
/// (keyword/intent matching, no external AI call) — swappable later for a
/// real AI service client (see Infrastructure/AI, not yet built) without
/// Application or the BotConversation domain model changing at all.
/// </summary>
public interface IBotReplyService
{
    BotReply Reply(string userMessage, string channel);
}
