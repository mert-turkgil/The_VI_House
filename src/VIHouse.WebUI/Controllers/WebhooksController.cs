using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// Stripe's delivery endpoint. Thin on purpose: verify the signature, hash the body, hand the
/// event to <see cref="IPaymentWebhookDispatcher"/>, translate its outcome into the status code
/// Stripe acts on. Everything that matters — idempotency, the transaction, the log row — lives in
/// the dispatcher, where a unit test can reach it.
///
/// Status codes are the contract with Stripe's retry schedule: 2xx means "done, never send this
/// again"; anything else means "send it again later". So a duplicate is a 200 (we have it), an
/// attempt still running elsewhere is a 409 (come back), a handler failure is a 500 (nothing was
/// committed; come back), and a bad signature is a 400 (not ours to process).
/// </summary>
[Route("webhooks")]
[ApiController]
[IgnoreAntiforgeryToken]
[EnableRateLimiting("webhook")]
public class WebhooksController(
    IPaymentProvider paymentProvider,
    IPaymentWebhookDispatcher dispatcher,
    ILogger<WebhooksController> logger) : ControllerBase
{
    /// <summary>Stripe events are a few kilobytes; a megabyte is already ten times the largest
    /// object it sends. Anything bigger is not Stripe.</summary>
    public const long MaxBodyBytes = 1_048_576;

    [HttpPost("stripe")]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Stripe(CancellationToken ct)
    {
        string json;
        using (var reader = new StreamReader(Request.Body))
            json = await reader.ReadToEndAsync(ct);

        var signature = Request.Headers["Stripe-Signature"].ToString();

        PaymentWebhookEvent webhookEvent;
        try
        {
            webhookEvent = paymentProvider.ConstructWebhookEvent(json, signature);
        }
        catch (Exception ex)
        {
            // Bad/missing signature — tell Stripe delivery failed (400) rather than silently
            // swallowing what could be a spoofed request (brief §32). The body is never logged.
            logger.LogWarning(ex, "Stripe webhook signature verification failed.");
            return BadRequest();
        }

        var result = await dispatcher.DispatchAsync(webhookEvent, Text.Sha256Hex(json), isReplay: false, ct);

        return result.Outcome switch
        {
            WebhookDispatchOutcome.Processed or WebhookDispatchOutcome.Ignored or WebhookDispatchOutcome.Duplicate => Ok(),
            WebhookDispatchOutcome.InProgress => StatusCode(StatusCodes.Status409Conflict),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
