using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// Development only: the catalogue of every transactional email, each rendered with sample data so
/// the templates can be looked at in a browser without triggering the thing that sends them. 404
/// everywhere else — the check is on the host environment, not on a flag, so it cannot be switched
/// on by mistake.
///
///   /dev/emails                → the catalogue: key, subject the sender uses, what triggers it
///   /dev/emails/{key}          → the rendered HTML, as the mail client would get it
///   /dev/emails/{key}.txt      → the plain-text alternative part the SMTP sender attaches
///
/// The list below is the one place a template, its subject and its trigger are written down side
/// by side; a template with no entry here has no preview, and the check at the bottom of the
/// catalogue says so.
/// </summary>
[AllowAnonymous]
[Route("dev/emails")]
public class DevEmailPreviewController(IEmailTemplateRenderer renderer, IWebHostEnvironment env) : Controller
{
    private static readonly DateTimeOffset Sample = new(2026, 10, 17, 18, 30, 0, TimeSpan.Zero);
    private const string Site = "https://thevihouse.com";

    private sealed record Entry(string Key, string Template, string Subject, string Trigger, string Group, Func<object> Model);

    private static readonly Entry[] Catalogue =
    [
        // --- Applications
        new("ApplicationReceived", "ApplicationReceived", "Application Received", "Visitor submits /apply", "Applications",
            () => new ApplicationReceivedEmailModel("Ada", "The VI House — Lisbon", "Lisbon")),
        new("ApplicationApproved", "ApplicationApproved", "You're approved — complete your booking", "Admin approves (or resends the invitation)", "Applications",
            () => new ApplicationApprovedEmailModel("Ada", "The VI House — Lisbon", "Lisbon", $"{Site}/invitation/VIH-7F3K9Q", Sample.AddDays(14))),
        new("ApplicationWaitlisted", "ApplicationWaitlisted", "You're on the waitlist", "Admin moves an application to the waitlist", "Applications",
            () => new ApplicationWaitlistedEmailModel("Ada", "The VI House — Lisbon")),
        new("ApplicationRejected", "ApplicationRejected", "About your application", "Admin rejects (reason shown only if typed)", "Applications",
            () => new ApplicationRejectedEmailModel("Ada", "The VI House — Lisbon", "Lisbon", "This round is built around later-stage operators; we'd love to see you for Berlin in the spring.", $"{Site}/experiences")),
        new("ApplicationRejectedNoReason", "ApplicationRejected", "About your application", "Same, without a note", "Applications",
            () => new ApplicationRejectedEmailModel("Ada", "The VI House — Lisbon", "Lisbon", null, $"{Site}/experiences")),
        new("ExperienceWaitlist", "ExperienceWaitlist", "You're on the waitlist", "Visitor joins a full experience's public waitlist", "Applications",
            () => new ExperienceWaitlistEmailModel("Ada", "The VI House — Lisbon", "Lisbon", 4)),

        // --- Bookings & payments
        new("BookingConfirmed", "BookingConfirmed", "You're confirmed — booking VI-26-0042", "Stripe webhook: experience checkout paid", "Bookings",
            () => new BookingConfirmedEmailModel("Ada", "VI-26-0042", "The VI House — Lisbon", "Lisbon", Sample, Sample.AddDays(3), 149900, "EUR")
            {
                Venue = "Palácio do Grilo, Lisbon", TimeZoneId = "Europe/Lisbon",
                TicketUrl = $"{Site}/account/bookings/VI-26-0042", ExperienceUrl = $"{Site}/experiences/lisbon-2026",
            }),
        new("PaymentFailed", "PaymentFailed", "We couldn't complete your payment", "Stripe webhook: experience checkout expired", "Bookings",
            () => new PaymentFailedEmailModel("Ada", "The VI House — Lisbon", $"{Site}/invitation/VIH-7F3K9Q")),
        new("ExperienceUpdate", "ExperienceUpdate", "The VI House — Lisbon: Venue confirmed", "Admin > Experience > Notify attendees", "Bookings",
            () => new ExperienceUpdateEmailModel("Ada", "The VI House — Lisbon", "Venue confirmed",
                "We open the doors at Palácio do Grilo at 18:00 on Friday.\n\nDress is smart; the first evening is a long dinner, so come hungry.", $"{Site}/experiences/lisbon-2026")),

        // --- Membership
        new("MembershipConfirmed", "MembershipConfirmed", "Welcome — you're a Founding Member", "Stripe webhook: membership checkout paid", "Membership",
            () => new MembershipConfirmedEmailModel("Ada", "Founding Member", Sample.AddYears(1))
            {
                AmountMinor = 150000, Currency = "GBP", MemberNumber = "VIH-3F2A9C10", AccountUrl = $"{Site}/account/membership",
            }),
        new("MembershipGranted", "MembershipConfirmed", "Welcome — you're a Member", "Admin grants a complimentary membership", "Membership",
            () => new MembershipConfirmedEmailModel("Ada", "Member", Sample.AddMonths(6))
            {
                Status = MembershipEmailStatus.Granted, MemberNumber = "VIH-3F2A9C10", AccountUrl = $"{Site}/account/membership",
            }),
        new("MembershipRenewed", "MembershipRenewed", "Your Member Monthly has renewed", "Stripe webhook: subscription invoice paid", "Membership",
            () => new MembershipRenewedEmailModel("Ada", "Member Monthly", Sample.AddMonths(1))),
        new("MembershipPaymentFailed", "MembershipPaymentFailed", "Your membership payment didn't go through", "Stripe webhook: renewal charge failed", "Membership",
            () => new MembershipPaymentFailedEmailModel("Ada", "Member Monthly", $"{Site}/account/membership", Sample.AddDays(9), Sample.AddDays(3))),
        new("MembershipEnded", "MembershipEnded", "Your membership has ended", "Stripe webhook: subscription ended at period end", "Membership",
            () => new MembershipEndedEmailModel("Ada", "Member Monthly", Sample, $"{Site}/membership", WasRevoked: false)),
        new("MembershipRevoked", "MembershipEnded", "Your membership has ended", "Admin revokes a complimentary membership", "Membership",
            () => new MembershipEndedEmailModel("Ada", "Member", Sample, $"{Site}/membership", WasRevoked: true)),
        new("MembershipResume", "MembershipResume", "Pick up where you left off", "Stripe webhook: /join checkout expired unpaid", "Membership",
            () => new MembershipResumeEmailModel("Ada", "Founding Member", $"{Site}/join/resume/K7M2P9")),

        // --- Sessions
        new("SeminarEnrolled", "SeminarEnrolled", "You're enrolled — Pricing for Founders", "Session enrolment confirmed (free, member or paid)", "Sessions",
            () => new SeminarEnrolledEmailModel("Ada", "Pricing for Founders", Sample, true, null, $"{Site}/sessions/pricing-for-founders")),
        new("SeminarEnrolledOnDemand", "SeminarEnrolled", "You're enrolled — How the House works", "Same, for an on-demand session", "Sessions",
            () => new SeminarEnrolledEmailModel("Ada", "How the House works", null, true, null, $"{Site}/sessions/how-the-house-works")),

        // --- Account & security
        new("ConfirmEmail", "ConfirmEmail", "Confirm your email", "Onboarding > send confirmation; Security > verify email", "Account",
            () => new ConfirmEmailAddressEmailModel("Ada", $"{Site}/confirm-email?userId=…&code=…")),
        new("EmailChange", "EmailChange", "Confirm your new email address", "Security & Login > change email (sent to the new address)", "Account",
            () => new EmailChangeEmailModel("Ada", "ada@newdomain.com", $"{Site}/confirm-email/change?userId=…&email=…&code=…")),
        new("PasswordReset", "PasswordReset", "Reset your password", "Forgot your password?", "Account",
            () => new PasswordResetEmailModel("Ada", $"{Site}/reset-password?code=…", 24)),
        new("WelcomeSetup", "WelcomeSetup", "Set up your VI House account", "A payment opened a new account", "Account",
            () => new WelcomeSetupEmailModel("Ada", $"{Site}/reset-password?code=…", "Founding Member")),
        new("SecurityAlert", "SecurityAlert", "New sign-in to your account", "Sign-in from an IP not seen in 30 days", "Account",
            () => new SecurityAlertEmailModel("Ada", "New sign-in to your account",
                "Your VI House account was just signed in to from an address it has not used in the last thirty days. If that was you — a new phone, a trip, a different network — there is nothing to do.",
                Sample, "81.2.69.160", "Safari on iPhone", $"{Site}/account/security/password", "Change your password")),
        new("SecurityAlertPassword", "SecurityAlert", "Your password was changed", "Password changed; also 2FA switched on/off/reset", "Account",
            () => new SecurityAlertEmailModel("Ada", "Your password was changed",
                "The password on your VI House account was just changed. If that was you, there is nothing to do.",
                Sample, "81.2.69.160", "Chrome on Windows", $"{Site}/forgot-password", "Reset your password")),
        new("AdminInvite", "AdminInvite", "Your VI House admin access", "SuperAdmin invites a staff account", "Account",
            () => new AdminInviteEmailModel("Ada", $"{Site}/reset-password?code=…", "Mert Türkgil", "Editor, Marketing")),

        // --- Ambassadors
        new("AmbassadorLink", "AmbassadorLink", "Your VI House referral link", "Admin > Ambassador > Send link", "Ambassadors",
            () => new AmbassadorLinkEmailModel("Anton", $"{Site}/r/ANTON", "ANTON", 15m, $"{Site}/ambassador", "Lovely to have you on board — here is your link for the launch post.")),
        new("ReferralConverted", "ReferralConverted", "Your referral link just worked", "Any referral conversion (application, approval, ticket, membership, session)", "Ambassadors",
            () => new ReferralConvertedEmailModel("Anton", "Someone who came through your link has bought a place on a session.", "£120.00", "£18.00", $"{Site}/ambassador")),

        // --- Internal
        new("ContactMessage", "ContactMessage", "New contact message from Ada Lovelace", "Public /contact form (to the site's contact address)", "Internal",
            () => new ContactMessageEmailModel("Ada Lovelace", "ada@example.com", "Speaking at Lisbon", "Hello,\n\nI'd love to talk about hosting a session on pricing at the Lisbon retreat.\n\nAda")),
    ];

    [HttpGet("")]
    public IActionResult Index()
    {
        if (!env.IsDevelopment()) return NotFound();

        var templatesOnDisk = Directory.GetFiles(Path.Combine(env.ContentRootPath, "Views", "Emails"), "*.cshtml")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null && !n.StartsWith('_'))
            .Select(n => n!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var covered = Catalogue.Select(e => e.Template).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = templatesOnDisk.Except(covered).OrderBy(n => n).ToList();

        var rows = Catalogue.GroupBy(e => e.Group).Select(g =>
            $"<h2>{g.Key}</h2><table><tr><th>Preview</th><th>Template</th><th>Subject</th><th>Trigger</th></tr>" +
            string.Join("", g.Select(e =>
                $"<tr><td><a href=\"/dev/emails/{e.Key}\">{e.Key}</a> · <a href=\"/dev/emails/{e.Key}.txt\">txt</a></td><td><code>{e.Template}.cshtml</code></td><td>{WebUtility.HtmlEncode(e.Subject)}</td><td>{WebUtility.HtmlEncode(e.Trigger)}</td></tr>")) +
            "</table>");

        var html = $@"<!doctype html><meta charset=utf-8><title>Email catalogue</title>
<style>body{{font:14px/1.6 system-ui;padding:32px;max-width:1100px;color:#12160f}}h1{{font-family:Georgia,serif}}h2{{margin-top:32px;font-size:15px;letter-spacing:.08em;text-transform:uppercase;color:#7a5f2e}}
table{{border-collapse:collapse;width:100%}}th,td{{text-align:left;padding:6px 10px;border-bottom:1px solid #e9e3d3;vertical-align:top}}th{{font-size:11px;text-transform:uppercase;letter-spacing:.08em;color:#63685c}}code{{font-size:12px}}a{{color:#00230a}}.warn{{background:#fdf3e7;padding:12px 16px;border-left:4px solid #c9873a;margin-top:24px}}</style>
<h1>Email catalogue</h1><p>{Catalogue.Length} previews over {covered.Count} templates in <code>Views/Emails</code>. Development only.</p>
{string.Join("", rows)}
{(missing.Count == 0 ? "<p class=warn style=\"border-color:#3f7d4e;background:#eef5ec\">Every template in the folder has a preview.</p>" : "<p class=warn>Templates with no preview: " + string.Join(", ", missing) + "</p>")}";
        return Content(html, "text/html");
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Show(string key, CancellationToken ct)
    {
        if (!env.IsDevelopment()) return NotFound();

        var asText = key.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
        if (asText) key = key[..^4];

        var entry = Catalogue.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return NotFound();

        var html = await renderer.RenderAsync(entry.Template, entry.Model(), ct);
        return asText
            ? Content(VIHouse.Business.Concrete.SmtpEmailSender.ToPlainText(html), "text/plain; charset=utf-8")
            : Content(html, "text/html");
    }
}
