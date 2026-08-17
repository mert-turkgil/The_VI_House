using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.WebUI.ViewModels.Content;

namespace VIHouse.WebUI.Controllers;

[Route("contact")]
public class ContactController(IEmailService emailService, IOptions<SiteOptions> siteOptions, IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = loc["Contact.Title"];
        return View(new ContactFormViewModel());
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("application-submit")]
    public async Task<IActionResult> Index(ContactFormViewModel form, CancellationToken ct)
    {
        ViewData["Title"] = loc["Contact.Title"];

        if (!ModelState.IsValid) return View(form);

        var recipient = siteOptions.Value.ContactEmail;
        if (!string.IsNullOrWhiteSpace(recipient))
        {
            await emailService.SendAsync(
                "ContactMessage", recipient, $"New contact message from {form.Name}",
                new ContactMessageEmailModel(form.Name.Trim(), form.Email.Trim(), form.Subject?.Trim(), form.Message.Trim()),
                ct: ct);
        }
        // If no ContactEmail is configured yet (Production default), the message still has nowhere to
        // go — but the visitor gets the same success page either way rather than a confusing error.

        ViewData["Sent"] = true;
        return View(new ContactFormViewModel());
    }
}
