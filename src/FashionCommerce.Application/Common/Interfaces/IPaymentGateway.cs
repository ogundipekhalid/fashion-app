namespace FashionCommerce.Application.Common.Interfaces;

public record InitializePaymentResult(bool Succeeded, string? AuthorizationUrl, string? ProviderReference, string? ErrorMessage)
{
    public static InitializePaymentResult Success(string authorizationUrl, string providerReference) =>
        new(true, authorizationUrl, providerReference, null);

    public static InitializePaymentResult Failure(string errorMessage) => new(false, null, null, errorMessage);
}

public record VerifyPaymentResult(bool Succeeded, bool IsPaid, decimal AmountPaid, string? Currency, string? RawResponseJson, string? ErrorMessage)
{
    public static VerifyPaymentResult Paid(decimal amount, string currency, string rawJson) => new(true, true, amount, currency, rawJson, null);
    public static VerifyPaymentResult NotPaid(string rawJson) => new(true, false, 0, null, rawJson, null);
    public static VerifyPaymentResult Failure(string errorMessage) => new(false, false, 0, null, null, errorMessage);
}

public record RefundPaymentResult(bool Succeeded, string? ProviderRefundReference, string? ErrorMessage)
{
    public static RefundPaymentResult Success(string providerRefundReference) => new(true, providerRefundReference, null);
    public static RefundPaymentResult Failure(string errorMessage) => new(false, null, errorMessage);
}

/// <summary>
/// One implementation per provider (Paystack, Flutterwave, Stripe) in
/// Infrastructure/Payments, resolved by PaymentProvider at the point an
/// order is placed. Every implementation talks in the platform's own
/// currency-and-amount terms; provider-specific quirks (Paystack wants
/// kobo not naira, Stripe wants cents not dollars, etc.) are handled
/// inside each implementation, never leaking into Application.
/// </summary>
public interface IPaymentGateway
{
    Domain.Enums.PaymentProvider Provider { get; }

    Task<InitializePaymentResult> InitializeAsync(string email, decimal amount, string currency, string reference, string callbackUrl, CancellationToken cancellationToken = default);

    Task<VerifyPaymentResult> VerifyAsync(string providerReference, CancellationToken cancellationToken = default);

    /// <summary>Issues a full or partial refund back to the original payment method.</summary>
    Task<RefundPaymentResult> RefundAsync(string providerReference, decimal amount, string reason, CancellationToken cancellationToken = default);

    /// <summary>Validates an inbound webhook's signature against the raw request body, using the provider's own scheme (HMAC-SHA512 for Paystack, etc.).</summary>
    bool VerifyWebhookSignature(string rawBody, string signatureHeader);
}

/// <summary>Resolves the right IPaymentGateway implementation for a given provider at runtime.</summary>
public interface IPaymentGatewayFactory
{
    IPaymentGateway Resolve(Domain.Enums.PaymentProvider provider);
}
