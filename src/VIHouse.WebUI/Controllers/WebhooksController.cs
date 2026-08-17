using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.Controllers;

[Route("webhooks")]
[ApiController]
[IgnoreAntiforgeryToken]
public class WebhooksController(IPaymentProvider paymentProvider, IPaymentService paymentService, ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpPost("stripe")]
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
            // swallowing what could be a spoofed request (brief §32).
            logger.LogWarning(ex, "Stripe webhook signature verification failed.");
            return BadRequest();
        }

        await paymentService.HandleWebhookEventAsync(webhookEvent, ct);
        return Ok();
    }
}
