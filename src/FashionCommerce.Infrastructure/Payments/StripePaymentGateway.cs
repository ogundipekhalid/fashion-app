using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Domain.Enums;
using Microsoft.Extensions.Options;

namespace FashionCommerce.Infrastructure.Payments;

public class StripeSettings
{
    public const string SectionName = "Payments:Stripe";
    public string SecretKey { get; set; } = default!;
    public string PublishableKey { get; set; } = default!;
    public string WebhookSecret { get; set; } = default!;
}

/// <summary>
/// Uses Stripe Checkout Sessions (hosted checkout page) rather than raw
/// PaymentIntents, so the shape matches Paystack/Flutterwave's
/// "AuthorizationUrl the customer is redirected to" pattern rather than
/// requiring a card-collection UI to be built client-side. Stripe's API is
/// form-encoded (application/x-www-form-urlencoded), not JSON — the one
/// real difference from the other two gateways' implementations.
/// </summary>
public class StripePaymentGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly StripeSettings _settings;

    public StripePaymentGateway(HttpClient httpClient, IOptions<StripeSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _httpClient.BaseAddress ??= new Uri("https://api.stripe.com/v1/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey);
    }

    public PaymentProvider Provider => PaymentProvider.Stripe;

    public async Task<InitializePaymentResult> InitializeAsync(string email, decimal amount, string currency, string reference, string callbackUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var minorUnitAmount = (long)Math.Round(amount * 100); // Stripe always wants the smallest currency unit (cents, not dollars)

            var form = new Dictionary<string, string>
            {
                ["mode"] = "payment",
                ["customer_email"] = email,
                ["client_reference_id"] = reference,
                ["success_url"] = $"{callbackUrl}?reference={Uri.EscapeDataString(reference)}",
                ["cancel_url"] = callbackUrl,
                ["line_items[0][price_data][currency]"] = currency,
                ["line_items[0][price_data][product_data][name]"] = "FashionCommerce order",
                ["line_items[0][price_data][unit_amount]"] = minorUnitAmount.ToString(),
                ["line_items[0][quantity]"] = "1",
                ["metadata[reference]"] = reference
            };

            var response = await _httpClient.PostAsync("checkout/sessions", new FormUrlEncodedContent(form), cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return InitializePaymentResult.Failure($"Stripe returned {response.StatusCode}: {rawJson}");

            var body = JsonSerializer.Deserialize<StripeCheckoutSession>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (body?.Url is null || body.Id is null)
                return InitializePaymentResult.Failure("Unexpected response from Stripe.");

            // The Checkout Session id (not our own `reference`) is what Stripe
            // needs for verification later, so it becomes the stored ProviderReference.
            return InitializePaymentResult.Success(body.Url, body.Id);
        }
        catch (Exception ex)
        {
            return InitializePaymentResult.Failure($"Payment initialization failed: {ex.Message}");
        }
    }

    public async Task<VerifyPaymentResult> VerifyAsync(string providerReference, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"checkout/sessions/{Uri.EscapeDataString(providerReference)}", cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return VerifyPaymentResult.Failure($"Stripe returned {response.StatusCode}.");

            var body = JsonSerializer.Deserialize<StripeCheckoutSession>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (body is null)
                return VerifyPaymentResult.Failure("Unexpected response from Stripe.");

            return body.PaymentStatus == "paid"
                ? VerifyPaymentResult.Paid((body.AmountTotal ?? 0) / 100m, body.Currency ?? "usd", rawJson)
                : VerifyPaymentResult.NotPaid(rawJson);
        }
        catch (Exception ex)
        {
            return VerifyPaymentResult.Failure($"Payment verification failed: {ex.Message}");
        }
    }

    public async Task<RefundPaymentResult> RefundAsync(string providerReference, decimal amount, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            // Refunds are keyed by PaymentIntent, not the Checkout Session id — look it up first.
            var sessionResponse = await _httpClient.GetAsync($"checkout/sessions/{Uri.EscapeDataString(providerReference)}", cancellationToken);
            var sessionJson = await sessionResponse.Content.ReadAsStringAsync(cancellationToken);
            var session = JsonSerializer.Deserialize<StripeCheckoutSession>(sessionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (session?.PaymentIntent is null)
                return RefundPaymentResult.Failure("Could not locate the original payment to refund.");

            var form = new Dictionary<string, string>
            {
                ["payment_intent"] = session.PaymentIntent,
                ["amount"] = ((long)Math.Round(amount * 100)).ToString(),
                ["reason"] = "requested_by_customer"
            };

            var response = await _httpClient.PostAsync("refunds", new FormUrlEncodedContent(form), cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return RefundPaymentResult.Failure($"Stripe refund failed: {response.StatusCode} — {rawJson}");

            var body = JsonSerializer.Deserialize<StripeRefund>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return body?.Id is null
                ? RefundPaymentResult.Failure("Unexpected response from Stripe.")
                : RefundPaymentResult.Success(body.Id);
        }
        catch (Exception ex)
        {
            return RefundPaymentResult.Failure($"Refund failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Stripe's signature scheme: the Stripe-Signature header contains
    /// "t={timestamp},v1={hmac}". The HMAC-SHA256 is computed over the
    /// string "{timestamp}.{rawBody}" using the webhook signing secret.
    /// </summary>
    public bool VerifyWebhookSignature(string rawBody, string signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(_settings.WebhookSecret))
            return false;

        var parts = signatureHeader.Split(',')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1]);

        if (!parts.TryGetValue("t", out var timestamp) || !parts.TryGetValue("v1", out var expectedSignature))
            return false;

        var signedPayload = $"{timestamp}.{rawBody}";
        var keyBytes = Encoding.UTF8.GetBytes(_settings.WebhookSecret);

        using var hmac = new HMACSHA256(keyBytes);
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature), Encoding.UTF8.GetBytes(expectedSignature));
    }

    private class StripeCheckoutSession
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("payment_status")] public string? PaymentStatus { get; set; }
        [JsonPropertyName("amount_total")] public long? AmountTotal { get; set; }
        [JsonPropertyName("currency")] public string? Currency { get; set; }
        [JsonPropertyName("payment_intent")] public string? PaymentIntent { get; set; }
    }

    private class StripeRefund
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
    }
}
