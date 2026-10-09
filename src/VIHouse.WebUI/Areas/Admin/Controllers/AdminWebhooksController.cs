using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// The Stripe event log (WebhookEvents): every delivery that verified, what it was about, and what
/// happened to it. The answer to "they say they paid" starts here — either the event is Processed
/// and the trail continues on the payment record, or it is Failed with the exception on it, or it
/// never arrived and the problem is upstream.
///
/// The one action is <see cref="Rerun"/>: fetch the event again from Stripe by id and push it
/// through the same dispatcher the endpoint uses, under the same transaction and idempotency
/// rules. Not a bypass — a handler that failed for a permanent reason fails again, visibly.
/// </summary>
[Authorize(Roles = AdminSections.RolesFor.Money)]
[Route("admin/webhooks")]
public class AdminWebhooksController(
    IWebhookEventRepository events,
    IPaymentProvider paymentProvider,
    IPaymentWebhookDispatcher dispatcher,
    IStringLocalizer<SharedResource> loc) : AdminControllerBase
{
    private const int PageSize = 100;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, CancellationToken ct)
    {
        WebhookEventStatus? parsed = Enum.TryParse<WebhookEventStatus>(status, true, out var s) ? s : null;
        return View(new AdminWebhookEventsViewModel
        {
            Status = parsed,
            Rows = await events.ListAsync(parsed, PageSize, ct),
            Counts = await events.CountByStatusAsync(ct),
            Take = PageSize,
        });
    }

    [HttpPost("{eventId}/rerun")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rerun(string eventId, CancellationToken ct)
    {
        var row = await events.GetAsync(eventId, ct);
        if (row is null) return NotFound();

        var webhookEvent = await paymentProvider.FetchWebhookEventAsync(eventId, ct);
        if (webhookEvent is null)
        {
            Status(loc["Admin.Webhooks.Msg.Gone", eventId].Value, isError: true);
            return RedirectToAction(nameof(Index));
        }

        // The hash on the row is the one from the original delivery; a re-run is the same event.
        var result = await dispatcher.DispatchAsync(webhookEvent, row.PayloadHash, isReplay: true, ct);
        Status(result.Outcome switch
        {
            WebhookDispatchOutcome.Processed => loc["Admin.Webhooks.Msg.Processed", eventId].Value,
            WebhookDispatchOutcome.Ignored => loc["Admin.Webhooks.Msg.Ignored", eventId, webhookEvent.RawType].Value,
            WebhookDispatchOutcome.InProgress => loc["Admin.Webhooks.Msg.InProgress", eventId].Value,
            WebhookDispatchOutcome.Failed => loc["Admin.Webhooks.Msg.Failed", eventId, result.Error ?? ""].Value,
            _ => loc["Admin.Webhooks.Msg.Nothing", eventId].Value,
        }, isError: result.Outcome == WebhookDispatchOutcome.Failed);
        return RedirectToAction(nameof(Index), new { status = row.Status == WebhookEventStatus.Failed ? "Failed" : null });
    }
}
