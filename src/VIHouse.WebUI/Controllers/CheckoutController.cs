using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.ViewModels.Checkout;

namespace VIHouse.WebUI.Controllers;

[Route("checkout")]
public class CheckoutController(IPaymentService paymentService, UserManager<ApplicationUser> userManager) : Controller
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

    [HttpGet("success")]
    public async Task<IActionResult> Success([FromQuery(Name = "session_id")] string sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return NotFound();

        var info = await paymentService.GetBookingConfirmationBySessionAsync(sessionId, ct);
        if (info is null) return NotFound();

        string? passwordSetupUrl = null;
        if (info.IsConfirmed && info.UserId is { } userId)
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is not null)
            {
                var rawToken = await userManager.GeneratePasswordResetTokenAsync(user);
                var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                passwordSetupUrl = Url.Page("/Account/ResetPassword", pageHandler: null,
                    values: new { area = "Identity", code = encodedCode }, protocol: Request.Scheme);
            }
        }

        ViewData["Title"] = info.IsConfirmed ? "Booking Confirmed" : "Processing Payment";
        return View(new CheckoutSuccessViewModel(info, passwordSetupUrl));
    }

    [HttpGet("cancel")]
    public IActionResult Cancel(string? code)
    {
        ViewData["Title"] = "Checkout Cancelled";
        ViewBag.InvitationCode = code;
        return View();
    }
}
