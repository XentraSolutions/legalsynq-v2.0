using System.Text;
using Commerce.Application.Payments.Abstractions;
using Commerce.Domain.Payments.Enums;
using Commerce.Infrastructure.Payments.Stripe;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Payments;

[ApiController]
[Route("api/commerce/payments/webhooks/stripe")]
public sealed class StripeWebhookController : ControllerBase
{
    private readonly IPaymentWebhookService _webhooks;

    public StripeWebhookController(IPaymentWebhookService webhooks) => _webhooks = webhooks;

    /// <summary>
    /// Receives Stripe webhook deliveries. Reads the raw HTTP body so
    /// the HMAC signature can be verified bit-for-bit. Always returns
    /// 200 once the event has been recorded — duplicates are not an
    /// error for Stripe and signing failures are surfaced as 400 by
    /// the underlying service.
    /// </summary>
    [HttpPost]
    [Consumes("application/json", "application/json; charset=utf-8")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var sig = Request.Headers.TryGetValue(StripeSignatureVerifier.HeaderName, out var sv)
            ? sv.ToString()
            : string.Empty;

        var result = await _webhooks.ReceiveAsync(PaymentProviderType.Stripe, rawBody, sig, ct);
        return Ok(new
        {
            eventLogId = result.EventLogId,
            status = result.Status.ToString(),
            reason = result.Reason
        });
    }
}
