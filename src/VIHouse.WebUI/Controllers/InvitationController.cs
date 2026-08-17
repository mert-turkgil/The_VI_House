using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.WebUI.ViewModels.Checkout;

namespace VIHouse.WebUI.Controllers;

[Route("invitation")]
public class InvitationController(IPaymentService paymentService) : Controller
{
    [HttpGet("{code}")]
    public async Task<IActionResult> Show(string code, CancellationToken ct)
    {
        var info = await paymentService.GetInvitationLandingAsync(code, ct);
        ViewData["Title"] = "Complete Your Booking";
        return View(new InvitationViewModel(code, info, TempData["CheckoutError"] as string));
    }
}
