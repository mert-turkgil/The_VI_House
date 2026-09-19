using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    IPaymentWebhookDispatcher dispatcher) : AdminControllerBase
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
            TempData["StatusMessage"] = $"Stripe no longer has event {eventId} (events are kept for 30 days), so it cannot be re-run.";
            return RedirectToAction(nameof(Index));
        }

        // The hash on the row is the one from the original delivery; a re-run is the same event.
        var result = await dispatcher.DispatchAsync(webhookEvent, row.PayloadHash, isReplay: true, ct);
        TempData["StatusMessage"] = result.Outcome switch
        {
            WebhookDispatchOutcome.Processed => $"Event {eventId} re-run and processed.",
            WebhookDispatchOutcome.Ignored => $"Event {eventId} re-run; no handler acts on {webhookEvent.RawType}.",
            WebhookDispatchOutcome.InProgress => $"Event {eventId} is being processed by another attempt right now.",
            WebhookDispatchOutcome.Failed => $"Event {eventId} failed again: {result.Error}",
            _ => $"Event {eventId}: nothing to do.",
        };
        return RedirectToAction(nameof(Index), new { status = row.Status == WebhookEventStatus.Failed ? "Failed" : null });
    }
}
