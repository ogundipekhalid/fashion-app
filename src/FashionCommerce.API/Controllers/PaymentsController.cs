using System.Text.Json;
using Asp.Versioning;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Payments.Commands.ConfirmPayment;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
[AllowAnonymous]
public class PaymentsController : ApiControllerBase
{
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;

    public PaymentsController(IPaymentGatewayFactory paymentGatewayFactory)
    {
        _paymentGatewayFactory = paymentGatewayFactory;
    }

    /// <summary>
    /// Hit by the client after the payment provider redirects back to
    /// CallbackUrl post-checkout. This is the "fast path" for confirming
    /// payment while the customer is still watching; the webhook below is
    /// the reliable server-to-server fallback in case the browser tab
    /// closes before this fires.
    /// </summary>
    [HttpGet("verify")]
    public async Task<IActionResult> Verify([FromQuery] string reference)
    {
        var result = await Mediator.Send(new ConfirmPaymentCommand(reference));
        return FromResult(result, "Payment verified.");
    }

    /// <summary>
    /// Paystack's server-to-server webhook. Signature is verified against
    /// the raw request body before anything is trusted — never parse and
    /// act on a webhook payload before this check.
    /// </summary>
    /// <summary>Flutterwave's webhook. Unlike Paystack's computed HMAC, Flutterwave just echoes back a pre-shared secret hash verbatim.</summary>
    [HttpPost("webhook/flutterwave")]
    public async Task<IActionResult> FlutterwaveWebhook()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var signature = Request.Headers["verif-hash"].FirstOrDefault() ?? string.Empty;
        var gateway = _paymentGatewayFactory.Resolve(PaymentProvider.Flutterwave);

        if (!gateway.VerifyWebhookSignature(rawBody, signature))
            return Unauthorized(ApiResponse.FailureResponse("Invalid webhook signature."));

        using var doc = JsonDocument.Parse(rawBody);
        var reference = doc.RootElement.GetProperty("data").GetProperty("tx_ref").GetString();

        if (!string.IsNullOrEmpty(reference))
            await Mediator.Send(new ConfirmPaymentCommand(reference));

        return Ok();
    }

    /// <summary>Stripe's webhook. Signature is "t={timestamp},v1={hmac-sha256}" in the Stripe-Signature header.</summary>
    [HttpPost("webhook/stripe")]
    public async Task<IActionResult> StripeWebhook()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;
        var gateway = _paymentGatewayFactory.Resolve(PaymentProvider.Stripe);

        if (!gateway.VerifyWebhookSignature(rawBody, signature))
            return Unauthorized(ApiResponse.FailureResponse("Invalid webhook signature."));

        using var doc = JsonDocument.Parse(rawBody);
        // Stripe's checkout.session.completed event nests the session (our ProviderReference) under data.object.id.
        var reference = doc.RootElement.GetProperty("data").GetProperty("object").GetProperty("id").GetString();

        if (!string.IsNullOrEmpty(reference))
            await Mediator.Send(new ConfirmPaymentCommand(reference));

        return Ok();
    }

    [HttpPost("webhook/paystack")]
    public async Task<IActionResult> PaystackWebhook()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var signature = Request.Headers["x-paystack-signature"].FirstOrDefault() ?? string.Empty;
        var gateway = _paymentGatewayFactory.Resolve(PaymentProvider.Paystack);

        if (!gateway.VerifyWebhookSignature(rawBody, signature))
            return Unauthorized(ApiResponse.FailureResponse("Invalid webhook signature."));

        using var doc = JsonDocument.Parse(rawBody);
        var reference = doc.RootElement.GetProperty("data").GetProperty("reference").GetString();

        if (string.IsNullOrEmpty(reference))
            return Ok(); // acknowledge receipt even if we can't act on it, so Paystack doesn't retry indefinitely

        await Mediator.Send(new ConfirmPaymentCommand(reference));

        return Ok(); // Paystack only cares about a 2xx response; failures are logged server-side, not surfaced here
    }
}
