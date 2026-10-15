namespace FashionCommerce.Application.Common.Interfaces;

public record ProductContentSuggestion(
    bool Succeeded, string? Description, string? SeoTitle, string? SeoDescription,
    List<string>? Tags, string? ErrorMessage)
{
    public static ProductContentSuggestion Success(string description, string seoTitle, string seoDescription, List<string> tags) =>
        new(true, description, seoTitle, seoDescription, tags, null);

    public static ProductContentSuggestion Failure(string errorMessage) => new(false, null, null, null, null, errorMessage);
}

public record AiTextResult(bool Succeeded, string? Text, string? ErrorMessage)
{
    public static AiTextResult Success(string text) => new(true, text, null);
    public static AiTextResult Failure(string errorMessage) => new(false, null, errorMessage);
}

/// <summary>
/// Provider-agnostic AI contract — same swappable pattern as
/// IPaymentGateway. GeminiAiService (Infrastructure) is the implementation
/// actually built; swapping to OpenAI/Anthropic/another provider later
/// means writing one new class against this interface, not touching any
/// caller of it.
/// </summary>
public interface IAiService
{
    /// <summary>Drafts a product description, SEO title/description, and tags from basic product facts — a starting point for the vendor to edit, never auto-published.</summary>
    Task<ProductContentSuggestion> GenerateProductContentAsync(string productName, string category, string? material, string? targetGender, CancellationToken cancellationToken = default);

    /// <summary>Condenses one review's free-text comment into a short one-line summary (populates ProductReview.AiSummary).</summary>
    Task<AiTextResult> SummarizeReviewAsync(string reviewComment, CancellationToken cancellationToken = default);

    /// <summary>Conversational styling advice for the AI Styling Assistant bot channel — freeform, unlike the rule-based support/dev-contact bots.</summary>
    Task<AiTextResult> GetStylingAdviceAsync(string userMessage, IReadOnlyList<string> conversationHistory, CancellationToken cancellationToken = default);
}
