using FashionCommerce.Application.Common.Interfaces;

namespace FashionCommerce.Application.Common.Services;

/// <summary>
/// Simple, transparent rule-based bot: matches keywords to intents and
/// returns a canned-but-helpful reply. Escalates to a human (SupportTicket)
/// whenever it detects a bug report, a request to speak to a developer, or
/// low confidence in its own understanding — the bot is deliberately
/// conservative about claiming it solved something it didn't.
/// </summary>
public class RuleBasedBotReplyService : IBotReplyService
{
    private static readonly (string[] Keywords, string Intent, string Reply, bool Escalate)[] Rules =
    {
        (new[] { "bug", "error", "crash", "broken", "not working", "doesn't work", "glitch" },
            "report_bug",
            "Sorry you're running into that. I've logged this as a bug report and it'll go straight to our support team — could you tell me a bit more about what you were doing when it happened?",
            true),

        (new[] { "developer", "talk to a human", "real person", "support agent", "contact you", "get in touch" },
            "contact_developer",
            "I'll connect you with our team. I've opened a support ticket for you — someone will follow up by email shortly.",
            true),

        (new[] { "refund", "money back", "return item" },
            "refund_question",
            "For refunds on a specific order, please use the 'Request Refund' option on that order's detail page — that routes it directly to the vendor and, if needed, our admin team. Want me to open a general support ticket instead?",
            false),

        (new[] { "payment failed", "payment not working", "card declined", "charged twice" },
            "payment_issue",
            "Payment issues need a closer look from our team — I've escalated this to a support ticket so someone can check the transaction on our end.",
            true),

        (new[] { "vendor", "become a seller", "sell on", "start selling" },
            "vendor_onboarding",
            "You can register as a vendor from the Sign Up page and choose 'Vendor'. Verification usually takes 1-2 business days after you submit your business details.",
            false),

        (new[] { "how do i", "how to", "where is", "where can i" },
            "how_to_question",
            "Happy to help — could you tell me a little more specifically what you're trying to do?",
            false),

        (new[] { "thank", "thanks", "thank you" },
            "gratitude",
            "You're welcome! Let me know if there's anything else I can help with.",
            false)
    };

    public BotReply Reply(string userMessage, string channel)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return new BotReply("Could you tell me a bit more about what you need help with?", null, false, 0);

        var normalized = userMessage.ToLowerInvariant();

        foreach (var (keywords, intent, reply, escalate) in Rules)
        {
            if (keywords.Any(k => normalized.Contains(k)))
                return new BotReply(reply, intent, escalate, 0.75);
        }

        // No rule matched — be honest about not understanding rather than
        // guessing, and offer the human escalation path.
        return new BotReply(
            "I'm not sure I fully understood that. I can open a support ticket so a member of our team can help directly — want me to do that?",
            "unrecognized",
            false,
            0.2);
    }
}
