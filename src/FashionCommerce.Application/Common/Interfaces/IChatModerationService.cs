namespace FashionCommerce.Application.Common.Interfaces;

public record ModerationResult(bool IsSuspicious, string? Reason)
{
    public static ModerationResult Clean() => new(false, null);
    public static ModerationResult Flagged(string reason) => new(true, reason);
}

/// <summary>
/// Scans an outgoing chat message — plus recent message history from the
/// same sender in the same conversation — for signs of an attempt to move
/// the transaction off-platform: phone numbers, bank account numbers, or
/// a number being spelled out one digit/word per message to dodge a
/// single-message regex check.
/// </summary>
public interface IChatModerationService
{
    ModerationResult CheckMessage(string content, IReadOnlyList<string> recentMessagesFromSameSender);
}
