using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VIHouse.Business.Abstract;
using VIHouse.WebUI.ViewModels.Checkout;

namespace VIHouse.WebUI.Controllers;

[Route("checkout")]
public class CheckoutController(IPaymentService paymentService, ICheckoutReconciliationService reconciliation) : Controller
{
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("checkout")]
    public async Task<IActionResult> Create(string code, Guid ticketTypeId, string? promoCode, CancellationToken ct)
    {
        var successUrl = Url.Action(nameof(Success), "Checkout", null, Request.Scheme)!;
        successUrl += (successUrl.Contains('?') ? "&" : "?") + "session_id={CHECKOUT_SESSION_ID}";
        var cancelUrl = Url.Action(nameof(Cancel), "Checkout", new { code }, Request.Scheme)!;

        var result = await paymentService.InitiateCheckoutAsync(code, ticketTypeId, promoCode, successUrl, cancelUrl, ct);
        if (!result.Success)
        {
            TempData["CheckoutError"] = result.Error;
            return RedirectToAction("Show", "Invitation", new { code });
        }

        return Redirect(result.CheckoutUrl!);
    }

    /// <summary>
    /// Where the provider sends the browser back to. The session id in the URL only says which
    /// checkout to ask the provider about; the provider's own answer, read server-side, is what
    /// confirms the booking — through the same path a webhook takes, so it happens once whichever
    /// arrives first. The page then shows local state. No password link is issued here: the setup
    /// link goes to the buyer's inbox, the one channel that is theirs.
    /// </summary>
    [HttpGet("success")]
    public async Task<IActionResult> Success([FromQuery(Name = "session_id")] string sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return NotFound();

        await reconciliation.ReconcileSessionAsync(sessionId, ct);

        var info = await paymentService.GetBookingConfirmationBySessionAsync(sessionId, ct);
        if (info is null) return NotFound();

        ViewData["Title"] = info.IsConfirmed ? "Booking Confirmed" : info.AwaitingBank ? "Payment In Progress" : "Processing Payment";
        return View(new CheckoutSuccessViewModel(info));
    }

    [HttpGet("cancel")]
    public IActionResult Cancel(string? code)
    {
        ViewData["Title"] = "Checkout Cancelled";
        ViewBag.InvitationCode = code;
        return View();
    }
}
