using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Domain.Enums;
using Microsoft.Extensions.Options;

namespace FashionCommerce.Infrastructure.Payments;

public class PaystackSettings
{
    public const string SectionName = "Payments:Paystack";
    public string SecretKey { get; set; } = default!;
    public string PublicKey { get; set; } = default!;
}

/// <summary>
/// Talks to Paystack's REST API directly over HttpClient (no third-party
/// SDK dependency — Paystack's API is simple enough that a thin wrapper is
/// more maintainable than a heavier SDK). Amounts are converted to kobo
/// (Paystack's smallest currency unit) at the boundary, never leaking that
/// detail into Application.
/// </summary>
public class PaystackPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly PaystackSettings _settings;

    public PaystackPaymentGateway(HttpClient httpClient, IOptions<PaystackSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _httpClient.BaseAddress ??= new Uri("https://api.paystack.co");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey);
    }

    public PaymentProvider Provider => PaymentProvider.Paystack;

    public async Task<InitializePaymentResult> InitializeAsync(string email, decimal amount, string currency, string reference, string callbackUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                email,
                amount = (long)(amount * 100), // naira -> kobo
                currency,
                reference,
                callback_url = callbackUrl
            };

            var response = await _httpClient.PostAsJsonAsync("/transaction/initialize", payload, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<PaystackInitializeResponse>(cancellationToken: cancellationToken);

            if (!response.IsSuccessStatusCode || body is null || !body.Status)
                return InitializePaymentResult.Failure(body?.Message ?? $"Paystack returned {response.StatusCode}.");

            return InitializePaymentResult.Success(body.Data!.AuthorizationUrl, body.Data.Reference);
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
            var response = await _httpClient.GetAsync($"/transaction/verify/{Uri.EscapeDataString(providerReference)}", cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return VerifyPaymentResult.Failure($"Paystack returned {response.StatusCode}.");

            var body = JsonSerializer.Deserialize<PaystackVerifyResponse>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (body?.Data is null)
                return VerifyPaymentResult.Failure("Unexpected response from Paystack.");

            return body.Data.Status == "success"
                ? VerifyPaymentResult.Paid(body.Data.Amount / 100m, body.Data.Currency, rawJson) // kobo -> naira
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
            var payload = new
            {
                transaction = providerReference,
                amount = (long)(amount * 100), // naira -> kobo
                merchant_note = reason
            };

            var response = await _httpClient.PostAsJsonAsync("/refund", payload, cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return RefundPaymentResult.Failure($"Paystack refund failed: {response.StatusCode} — {rawJson}");

            var body = JsonSerializer.Deserialize<PaystackRefundResponse>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (body?.Data is null)
                return RefundPaymentResult.Failure("Unexpected response from Paystack.");

            return RefundPaymentResult.Success(body.Data.Id.ToString());
        }
        catch (Exception ex)
        {
            return RefundPaymentResult.Failure($"Refund failed: {ex.Message}");
        }
    }

    /// <summary>Paystack signs webhooks with HMAC-SHA512 of the raw body using the secret key, sent in the x-paystack-signature header.</summary>
    public bool VerifyWebhookSignature(string rawBody, string signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader)) return false;

        var keyBytes = Encoding.UTF8.GetBytes(_settings.SecretKey);
        var bodyBytes = Encoding.UTF8.GetBytes(rawBody);

        using var hmac = new HMACSHA512(keyBytes);
        var computedHash = hmac.ComputeHash(bodyBytes);
        var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature), Encoding.UTF8.GetBytes(signatureHeader.ToLowerInvariant()));
    }

    private class PaystackInitializeResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public bool Status { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string? Message { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public PaystackInitializeData? Data { get; set; }
    }

    private class PaystackInitializeData
    {
        [System.Text.Json.Serialization.JsonPropertyName("authorization_url")]
        public string AuthorizationUrl { get; set; } = default!;

        [System.Text.Json.Serialization.JsonPropertyName("reference")]
        public string Reference { get; set; } = default!;
    }

    private class PaystackVerifyResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public PaystackVerifyData? Data { get; set; }
    }

    private class PaystackVerifyData
    {
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = default!;

        [System.Text.Json.Serialization.JsonPropertyName("amount")]
        public long Amount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("currency")]
        public string Currency { get; set; } = default!;
    }

    private class PaystackRefundResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public PaystackRefundData? Data { get; set; }
    }

    private class PaystackRefundData
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public long Id { get; set; }
    }
}
