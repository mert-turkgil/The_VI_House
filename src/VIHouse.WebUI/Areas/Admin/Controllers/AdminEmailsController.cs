using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    ISmsService smsService) : AdminControllerBase
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
