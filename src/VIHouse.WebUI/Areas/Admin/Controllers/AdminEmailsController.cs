using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Identity;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Communication;
using VIHouse.WebUI.Areas.Admin.ViewModels;

using VIHouse.WebUI.Areas.Admin;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// The transactional message log (brief §57, §71), both channels.
///
/// It answers one question that used to need a database client: "did the approval actually reach
/// them?". An applicant who says they never got their payment link is either looking at a Failed row
/// with a provider error on it, or at a Sent row and their own spam folder, and those are very
/// different conversations. Since the link now goes out by text as well, both channels live here.
///
/// Nothing here can be edited or deleted — it is an audit trail. The one action is Resend on a
/// failed message, which sends the stored copy again as a NEW row; the failure stays on record,
/// marked resent. The copy itself is never shown on this screen, so it cannot leak the contents of
/// anyone's mail or the single-use invitation URL inside a text message.
/// </summary>
[Authorize(Roles = AdminSections.RolesFor.Communications)]
[Route("admin/emails")]
public class AdminEmailsController(
    IEmailLogRepository emailLogs,
    ISmsLogRepository smsLogs,
    IEmailService emailService,
    ISmsService smsService,
    IOptionsSnapshot<SmtpOptions> smtp,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> loc) : AdminControllerBase
{
    private const int PageSize = 50;

    [HttpGet("")]

    public async Task<IActionResult> Index(string? channel, string? status, int page, CancellationToken ct)
    {
        // An unparseable status or channel is treated as the default rather than an error — both
        // arrive from a query string, and a stale link should show the log, not a 400.
        EmailStatus? parsed = Enum.TryParse<EmailStatus>(status, out var s) ? s : null;
        var showSms = string.Equals(channel, "sms", StringComparison.OrdinalIgnoreCase);
        var current = Math.Max(page, 1);
        var skip = (current - 1) * PageSize;

        var model = new AdminEmailLogViewModel
        {
            ShowSms = showSms,
            SmsConfigured = smsService.IsConfigured,
            SmtpProblems = smtp.Value.Problems(),
            SmtpSummary = smtp.Value.IsConfigured
                ? (string.IsNullOrWhiteSpace(smtp.Value.Username)
                    ? loc["Admin.Emails.SmtpSummaryAnonymous", $"{smtp.Value.Host}:{smtp.Value.Port}", smtp.Value.FromEmail].Value
                    : loc["Admin.Emails.SmtpSummary", $"{smtp.Value.Host}:{smtp.Value.Port}", smtp.Value.FromEmail, smtp.Value.Username].Value)
                : null,
            Status = parsed,
            Page = current,
            PageSize = PageSize,
        };

        // "Failed" in the banners means still outstanding: a failure that has been resent is history.
        var openEmailFailures = await emailLogs.CountAsync(e => e.Status == EmailStatus.Failed && e.ResentAt == null, ct);
        var openSmsFailures = await smsLogs.CountAsync(e => e.Status == EmailStatus.Failed && e.ResentAt == null, ct);

        if (showSms)
        {
            model.SmsRows = await smsLogs.GetRecentAsync(parsed, skip, PageSize, ct);
            model.TotalCount = await smsLogs.CountAsync(parsed, ct);
            model.FailedCount = openSmsFailures;
            model.OtherChannelFailedCount = openEmailFailures;
        }
        else
        {
            model.Rows = await emailLogs.GetRecentAsync(parsed, skip, PageSize, ct);
            model.TotalCount = await emailLogs.CountAsync(parsed, ct);
            model.FailedCount = openEmailFailures;
            model.OtherChannelFailedCount = openSmsFailures;
        }

        return View(model);
    }

    /// <summary>
    /// Sends a short message to the signed-in admin and reports exactly what the server said. The
    /// fastest way to tell "email is broken" from "this one address bounced".
    /// </summary>
    [HttpPost("test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTest(string? to, CancellationToken ct)
    {
        var me = await userManager.FindByIdAsync(CurrentAdminId().ToString());
        var recipient = string.IsNullOrWhiteSpace(to) ? me?.Email : to.Trim();
        if (string.IsNullOrWhiteSpace(recipient) || !recipient.Contains('@'))
        {
            Status(loc["Admin.Emails.Msg.TestNeedsAddress"].Value, isError: true);
            return RedirectToAction(nameof(Index));
        }

        var sent = await emailService.SendAsync("TestEmail", recipient, "The VI House — test email",
            new TestEmailModel(me?.Email ?? "an administrator", DateTimeOffset.UtcNow, smtp.Value.Host),
            SiteCultures.Default, ct: ct);

        if (sent)
        {
            Status(loc["Admin.Emails.Msg.TestAccepted", smtp.Value.Host, recipient].Value);
        }
        else
        {
            // The server's own words, untranslated: they are what tells a bad password from a dead host.
            var latest = (await emailLogs.GetRecentAsync(EmailStatus.Failed, 0, 1, ct)).FirstOrDefault();
            Status(loc["Admin.Emails.Msg.TestFailed", recipient, latest?.ErrorMessage ?? "?"].Value, isError: true);
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Resends every outstanding failure from the last week — for after the mail server
    /// has been fixed, so nobody has to click Resend row by row.</summary>
    [HttpPost("retry-failed")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryFailed(CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-7);
        var failed = (await emailLogs.FindAsync(e => e.Status == EmailStatus.Failed && e.ResentAt == null && e.Body != null && e.CreatedAt >= since, ct))
            .OrderBy(e => e.CreatedAt).Take(200).ToList();

        int ok = 0, again = 0;
        foreach (var row in failed)
        {
            var result = await emailService.ResendAsync(row.Id, ct);
            if (result.Sent) ok++; else again++;
            // A failure after one success usually means a per-recipient problem; a failure on the
            // very first means the server is still down, and hammering it helps nobody.
            if (ok == 0 && again >= 3) break;
        }

        Status(failed.Count == 0
            ? loc["Admin.Emails.Msg.NothingToRetry"].Value
            : loc["Admin.Emails.Msg.Retried", ok + again, failed.Count, ok, again].Value, isError: again > 0);
        return RedirectToAction(nameof(Index), new { status = again > 0 ? nameof(EmailStatus.Failed) : null });
    }

    [HttpPost("{id:guid}/resend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendEmail(Guid id, string? status, CancellationToken ct)
    {
        var result = await emailService.ResendAsync(id, ct);
        Status(result.Message, isError: !result.Sent);
        return RedirectToAction(nameof(Index), new { status });
    }

    [HttpPost("sms/{id:guid}/resend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendSms(Guid id, string? status, CancellationToken ct)
    {
        var result = await smsService.ResendAsync(id, ct);
        Status(result.Message, isError: !result.Sent);
        return RedirectToAction(nameof(Index), new { channel = "sms", status });
    }
}
