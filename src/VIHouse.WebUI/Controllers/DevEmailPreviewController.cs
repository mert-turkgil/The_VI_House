using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// Development only: renders each transactional email with sample data so the templates can be
/// looked at in a browser without triggering the thing that sends them. 404 everywhere else —
/// the check is on the host environment, not on a flag, so it cannot be switched on by mistake.
///
///   /dev/emails                → the list
///   /dev/emails/{template}     → the rendered HTML, as the mail client would get it
/// </summary>
[AllowAnonymous]
[Route("dev/emails")]
public class DevEmailPreviewController(IEmailTemplateRenderer renderer, IWebHostEnvironment env) : Controller
{
    private static readonly DateTimeOffset Sample = new(2026, 10, 17, 18, 30, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, Func<object>> Samples = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MembershipConfirmed"] = () => new MembershipConfirmedEmailModel("Ada", "Founding Member", Sample.AddYears(1))
        {
            AmountMinor = 150000, Currency = "GBP", MemberNumber = "VIH-3F2A9C10", AccountUrl = "https://thevihouse.com/account/membership",
        },
        ["MembershipGranted"] = () => new MembershipConfirmedEmailModel("Ada", "Member", Sample.AddMonths(6))
        {
            Status = MembershipEmailStatus.Granted, MemberNumber = "VIH-3F2A9C10", AccountUrl = "https://thevihouse.com/account/membership",
        },
        ["MembershipRenewed"] = () => new MembershipRenewedEmailModel("Ada", "Member Monthly", Sample.AddMonths(1)),
        ["MembershipPaymentFailed"] = () => new MembershipPaymentFailedEmailModel("Ada", "Member Monthly", "https://thevihouse.com/account/membership", Sample.AddDays(9), Sample.AddDays(3)),
        ["BookingConfirmed"] = () => new BookingConfirmedEmailModel("Ada", "VIH26-0042", "The VI House — Lisbon", "Lisbon", Sample, Sample.AddDays(3), 149900, "EUR")
        {
            Venue = "Palácio do Grilo, Lisbon", TimeZoneId = "Europe/Lisbon",
            TicketUrl = "https://thevihouse.com/account/bookings/VIH26-0042", ExperienceUrl = "https://thevihouse.com/experiences/lisbon-2026",
        },
        ["SeminarEnrolled"] = () => new SeminarEnrolledEmailModel("Ada", "Pricing for Founders", Sample, true, null, "https://thevihouse.com/sessions/pricing-for-founders"),
        ["SecurityAlert"] = () => new SecurityAlertEmailModel("Ada", "New sign-in to your account",
            "Your VI House account was just signed in to from an address it has not used in the last thirty days. If that was you — a new phone, a trip, a different network — there is nothing to do.",
            Sample, "81.2.69.160", "Safari on iPhone", "https://thevihouse.com/Identity/Account/Manage/ChangePassword", "Change your password"),
        ["SecurityAlertPassword"] = () => new SecurityAlertEmailModel("Ada", "Your password was changed",
            "The password on your VI House account was just changed. If that was you, there is nothing to do.",
            Sample, "81.2.69.160", "Chrome on Windows", "https://thevihouse.com/Identity/Account/ForgotPassword", "Reset your password"),
    };

    private static string TemplateFor(string key) => key switch
    {
        "MembershipGranted" => "MembershipConfirmed",
        "SecurityAlertPassword" => "SecurityAlert",
        _ => key,
    };

    [HttpGet("")]
    public IActionResult Index()
    {
        if (!env.IsDevelopment()) return NotFound();
        var links = string.Join("", Samples.Keys.Select(k => $"<li><a href=\"/dev/emails/{k}\">{k}</a></li>"));
        return Content($"<!doctype html><meta charset=utf-8><title>Email previews</title><body style=\"font:15px/1.6 system-ui;padding:32px\"><h1>Email previews</h1><ul>{links}</ul>", "text/html");
    }

    [HttpGet("{template}")]
    public async Task<IActionResult> Show(string template, CancellationToken ct)
    {
        if (!env.IsDevelopment()) return NotFound();
        if (!Samples.TryGetValue(template, out var sample)) return NotFound();
        var html = await renderer.RenderAsync(TemplateFor(template), sample(), ct);
        return Content(html, "text/html");
    }
}
