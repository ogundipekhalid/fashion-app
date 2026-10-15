using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using FashionCommerce.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace FashionCommerce.Infrastructure.AI;

public class GeminiSettings
{
    public const string SectionName = "AI:Gemini";
    public string ApiKey { get; set; } = default!;

    /// <summary>"gemini-1.5-flash" is the free-tier-friendly model — fast and cheap/free rather than the larger Pro models.</summary>
    public string Model { get; set; } = "gemini-1.5-flash";
}

/// <summary>
/// Talks to Google's Gemini API (generativelanguage.googleapis.com) over
/// plain HttpClient — no SDK dependency, consistent with how the payment
/// gateways were built. Chosen specifically because Gemini has a genuine
/// free tier (rate-limited, no credit card required), unlike OpenAI or
/// Anthropic which are pay-per-use from the first request.
/// </summary>
public class GeminiAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;

    public GeminiAiService(HttpClient httpClient, IOptions<GeminiSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _httpClient.BaseAddress ??= new Uri("https://generativelanguage.googleapis.com/v1beta/");
    }

    public async Task<ProductContentSuggestion> GenerateProductContentAsync(
        string productName, string category, string? material, string? targetGender, CancellationToken cancellationToken = default)
    {
        var prompt = $"""
            You are writing e-commerce product listing content for a fashion marketplace.

            Product name: {productName}
            Category: {category}
            Material: {material ?? "not specified"}
            Target gender: {targetGender ?? "unisex"}

            Respond with ONLY a JSON object (no markdown, no code fences) in exactly this shape:
            Respond with these JSON properties: description (2-3 sentence appealing product description),
            seoTitle (under 60 characters), seoDescription (under 155 characters), and tags (five strings).
            """;

        var result = await CallGeminiAsync(prompt, cancellationToken);
        if (!result.Succeeded)
            return ProductContentSuggestion.Failure(result.ErrorMessage!);

        try
        {
            var cleaned = StripMarkdownCodeFence(result.Text!);
            var parsed = JsonSerializer.Deserialize<GeneratedProductContent>(cleaned, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed is null)
                return ProductContentSuggestion.Failure("AI response could not be parsed.");

            return ProductContentSuggestion.Success(
                parsed.Description ?? string.Empty,
                parsed.SeoTitle ?? productName,
                parsed.SeoDescription ?? string.Empty,
                parsed.Tags ?? new List<string>());
        }
        catch (JsonException)
        {
            return ProductContentSuggestion.Failure("AI returned a response that wasn't valid JSON. Try again.");
        }
    }

    public async Task<AiTextResult> SummarizeReviewAsync(string reviewComment, CancellationToken cancellationToken = default)
    {
        var prompt = $"""
            Summarize this product review in one short, honest sentence (under 15 words).
            Do not add opinions not present in the original review.

            Review: "{reviewComment}"

            Respond with ONLY the one-sentence summary, no quotes, no preamble.
            """;

        return await CallGeminiAsync(prompt, cancellationToken);
    }

    public async Task<AiTextResult> GetStylingAdviceAsync(string userMessage, IReadOnlyList<string> conversationHistory, CancellationToken cancellationToken = default)
    {
        var historyBlock = conversationHistory.Count > 0
            ? "Conversation so far:\n" + string.Join("\n", conversationHistory) + "\n\n"
            : string.Empty;

        var prompt = $"""
            You are a friendly, concise fashion styling assistant for FashionCommerce, a fashion marketplace.
            Give practical outfit and styling advice. Keep responses to 2-4 sentences. Don't invent specific
            product names or prices you don't actually know — speak in general styling terms instead.

            {historyBlock}Customer: {userMessage}

            Respond with ONLY your reply to the customer, no preamble.
            """;

        return await CallGeminiAsync(prompt, cancellationToken);
    }

    private async Task<AiTextResult> CallGeminiAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"models/{_settings.Model}:generateContent?key={_settings.ApiKey}", payload, cancellationToken);

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return AiTextResult.Failure($"Gemini returned {response.StatusCode}: {rawJson}");

            var body = JsonSerializer.Deserialize<GeminiResponse>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var text = body?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            return string.IsNullOrWhiteSpace(text)
                ? AiTextResult.Failure("Gemini returned an empty response (the prompt may have been blocked by safety filters).")
                : AiTextResult.Success(text.Trim());
        }
        catch (Exception ex)
        {
            return AiTextResult.Failure($"AI request failed: {ex.Message}");
        }
    }

    private static string StripMarkdownCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
                return trimmed[(firstNewline + 1)..lastFence].Trim();
        }
        return trimmed;
    }

    private class GeneratedProductContent
    {
        public string? Description { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public List<string>? Tags { get; set; }
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")] public List<GeminiCandidate>? Candidates { get; set; }
    }

    private class GeminiCandidate
    {
        [JsonPropertyName("content")] public GeminiContent? Content { get; set; }
    }

    private class GeminiContent
    {
        [JsonPropertyName("parts")] public List<GeminiPart>? Parts { get; set; }
    }

    private class GeminiPart
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
    }
}
