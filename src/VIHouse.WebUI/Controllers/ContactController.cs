using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.WebUI.ViewModels.Content;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Controllers;

[Route("contact")]
public class ContactController(IEmailService emailService, SeoResolver seo, IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = loc["Contact.Title"];
        this.SetSeo(loc["Seo.Contact.Description"].Value, canonicalPath: "/contact");
        return View(new ContactFormViewModel());
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("application-submit")]
    public async Task<IActionResult> Index(ContactFormViewModel form, CancellationToken ct)
    {
        ViewData["Title"] = loc["Contact.Title"];

        if (!ModelState.IsValid) return View(form);

        // SiteSetting.ContactEmail, not SiteOptions — the same source the Contact page's own info
        // panel, the coming-soon footer, and the JSON-LD block all read, edited in one place
        // (Admin > Site & SEO). Two independently-configurable "contact email" values meant an
        // admin could change one and have this form keep mailing the address they just replaced.
        var settings = await seo.SettingsAsync(ct);
        var recipient = settings.ContactEmail;
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
