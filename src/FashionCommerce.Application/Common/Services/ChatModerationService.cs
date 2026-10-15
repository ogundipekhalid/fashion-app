using System.Text.RegularExpressions;
using FashionCommerce.Application.Common.Interfaces;

namespace FashionCommerce.Application.Common.Services;

/// <summary>
/// Deliberately conservative and rule-based rather than ML-based — false
/// positives just mean an admin reviews a legitimate conversation, but false
/// negatives mean a circumvention attempt slips through, so the rules lean
/// toward catching more. Every trigger produces a human-readable reason so
/// the admin reviewing a blocked conversation understands why it fired.
/// </summary>
public class ChatModerationService : IChatModerationService
{
    // 7+ digits in a row (allowing spaces/dashes/dots between groups) — covers
    // Nigerian mobile numbers (11 digits), NUBAN account numbers (10 digits),
    // and most international formats.
    private static readonly Regex DigitSequenceRegex = new(@"(\d[\s\-\.]?){7,}", RegexOptions.Compiled);

    // Common ways people try to sneak a phone/account number past a naive filter.
    private static readonly Regex ObfuscatedDigitsRegex = new(
        @"\b(o|O|zero|one|two|three|four|five|six|seven|eight|nine|oh)\b[\s,\-]*(\b(o|O|zero|one|two|three|four|five|six|seven|eight|nine|oh)\b[\s,\-]*){6,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ContactKeywordRegex = new(
        @"\b(whatsapp|whats app|wa\.me|telegram|call me|text me|reach me|my number|phone number|account number|acc\s?no|nuban|bank transfer|send to my|pay me directly|off[\s\-]?platform|outside the app)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "Single digit per message" evasion: sender sends "0", then "8", then "0", then "1"...
    // building a number one message at a time so no single message trips the regex above.
    private static readonly Regex SingleTokenRegex = new(@"^\s*(\d{1,2}|o|O|zero|one|two|three|four|five|six|seven|eight|nine|oh)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const int MinConsecutiveShortNumericMessages = 4;

    public ModerationResult CheckMessage(string content, IReadOnlyList<string> recentMessagesFromSameSender)
    {
        if (string.IsNullOrWhiteSpace(content))
            return ModerationResult.Clean();

        var digitCount = content.Count(char.IsDigit);

        if (DigitSequenceRegex.IsMatch(content) || digitCount >= 7)
            return ModerationResult.Flagged("Message appears to contain a phone number or account number.");

        if (ObfuscatedDigitsRegex.IsMatch(content))
            return ModerationResult.Flagged("Message appears to spell out a number to avoid detection.");

        if (ContactKeywordRegex.IsMatch(content))
            return ModerationResult.Flagged("Message references sharing contact details or moving the transaction off-platform.");

        // Check for the "one digit/word per message" pattern across recent history + this message.
        var window = recentMessagesFromSameSender.Append(content).TakeLast(MinConsecutiveShortNumericMessages).ToList();
        if (window.Count == MinConsecutiveShortNumericMessages && window.All(m => SingleTokenRegex.IsMatch(m)))
            return ModerationResult.Flagged("Sender appears to be spelling out a number one message at a time to avoid detection.");

        return ModerationResult.Clean();
    }
}
