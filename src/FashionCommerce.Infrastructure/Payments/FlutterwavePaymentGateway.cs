using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Domain.Enums;
using Microsoft.Extensions.Options;

namespace FashionCommerce.Infrastructure.Payments;

public class FlutterwaveSettings
{
    public const string SectionName = "Payments:Flutterwave";
    public string SecretKey { get; set; } = default!;
    public string PublicKey { get; set; } = default!;

    /// <summary>Flutterwave webhooks are verified against a pre-shared "secret hash" you set in their dashboard (not a computed HMAC) — sent back verbatim in the "verif-hash" header.</summary>
    public string WebhookSecretHash { get; set; } = default!;
}

/// <summary>
/// Talks to Flutterwave's REST API (v3) directly over HttpClient, same
/// approach as PaystackPaymentGateway — no third-party SDK. Amounts are
/// naira throughout (Flutterwave, unlike Paystack, doesn't use a
/// subunit — no kobo conversion needed here).
/// </summary>
public class FlutterwavePaymentGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly FlutterwaveSettings _settings;

    public FlutterwavePaymentGateway(HttpClient httpClient, IOptions<FlutterwaveSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _httpClient.BaseAddress ??= new Uri("https://api.flutterwave.com/v3/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey);
    }

    public PaymentProvider Provider => PaymentProvider.Flutterwave;

    public async Task<InitializePaymentResult> InitializeAsync(string email, decimal amount, string currency, string reference, string callbackUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                tx_ref = reference,
                amount = amount.ToString("F2"),
                currency,
                redirect_url = callbackUrl,
                customer = new { email }
            };

            var response = await _httpClient.PostAsJsonAsync("payments", payload, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<FlutterwaveInitializeResponse>(cancellationToken: cancellationToken);

            if (!response.IsSuccessStatusCode || body?.Status != "success" || body.Data is null)
                return InitializePaymentResult.Failure(body?.Message ?? $"Flutterwave returned {response.StatusCode}.");

            return InitializePaymentResult.Success(body.Data.Link, reference); // Flutterwave confirms the transaction by tx_ref/id at verify time, not a provider-issued reference here
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
            // Flutterwave verifies by tx_ref via a query-based lookup endpoint.
            var response = await _httpClient.GetAsync($"transactions/verify_by_reference?tx_ref={Uri.EscapeDataString(providerReference)}", cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return VerifyPaymentResult.Failure($"Flutterwave returned {response.StatusCode}.");

            var body = JsonSerializer.Deserialize<FlutterwaveVerifyResponse>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (body?.Data is null)
                return VerifyPaymentResult.Failure("Unexpected response from Flutterwave.");

            return body.Data.Status == "successful"
                ? VerifyPaymentResult.Paid(body.Data.Amount, body.Data.Currency, rawJson)
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
            // Flutterwave refunds are keyed by the numeric transaction id, not tx_ref — look it up first.
            var verifyResponse = await _httpClient.GetAsync($"transactions/verify_by_reference?tx_ref={Uri.EscapeDataString(providerReference)}", cancellationToken);
            var verifyJson = await verifyResponse.Content.ReadAsStringAsync(cancellationToken);
            var verifyBody = JsonSerializer.Deserialize<FlutterwaveVerifyResponse>(verifyJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (verifyBody?.Data is null)
                return RefundPaymentResult.Failure("Could not locate the original transaction to refund.");

            var payload = new { amount };
            var response = await _httpClient.PostAsJsonAsync($"transactions/{verifyBody.Data.Id}/refund", payload, cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return RefundPaymentResult.Failure($"Flutterwave refund failed: {response.StatusCode} — {rawJson}");

            var body = JsonSerializer.Deserialize<FlutterwaveRefundResponse>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (body?.Data is null)
                return RefundPaymentResult.Failure("Unexpected response from Flutterwave.");

            return RefundPaymentResult.Success(body.Data.Id.ToString());
        }
        catch (Exception ex)
        {
            return RefundPaymentResult.Failure($"Refund failed: {ex.Message}");
        }
    }

    /// <summary>Flutterwave doesn't compute an HMAC — it echoes back a pre-shared secret hash verbatim in the "verif-hash" header, which you compare directly.</summary>
    public bool VerifyWebhookSignature(string rawBody, string signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(_settings.WebhookSecretHash))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signatureHeader), Encoding.UTF8.GetBytes(_settings.WebhookSecretHash));
    }

    private class FlutterwaveInitializeResponse
    {
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("data")] public FlutterwaveInitializeData? Data { get; set; }
    }

    private class FlutterwaveInitializeData
    {
        [JsonPropertyName("link")] public string Link { get; set; } = default!;
    }

    private class FlutterwaveVerifyResponse
    {
        [JsonPropertyName("data")] public FlutterwaveVerifyData? Data { get; set; }
    }

    private class FlutterwaveVerifyData
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = default!;
        [JsonPropertyName("amount")] public decimal Amount { get; set; }
        [JsonPropertyName("currency")] public string Currency { get; set; } = default!;
    }

    private class FlutterwaveRefundResponse
    {
        [JsonPropertyName("data")] public FlutterwaveRefundData? Data { get; set; }
    }

    private class FlutterwaveRefundData
    {
        [JsonPropertyName("id")] public long Id { get; set; }
    }
}
