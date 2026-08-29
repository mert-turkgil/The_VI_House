using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Communication;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// The transactional message log (brief §57, §71) — read-only by design, both channels.
///
/// It answers one question that used to need a database client: "did the approval actually reach
/// them?". An applicant who says they never got their payment link is either looking at a Failed row
/// with a provider error on it, or at a Sent row and their own spam folder, and those are very
/// different conversations. Since the link now goes out by text as well, both channels live here.
///
/// Nothing here can be edited or deleted. It is an audit trail; a log an admin can tidy is not one.
/// Neither table stores a body — only recipient, subject and template — so this screen cannot leak
/// the contents of anyone's mail, or the single-use invitation URL inside a text message.
/// </summary>
public class AdminEmailsController(
    IEmailLogRepository emailLogs,
    ISmsLogRepository smsLogs,
    ISmsService smsService) : AdminControllerBase
{
    private const int PageSize = 50;

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

        if (showSms)
        {
            model.SmsRows = await smsLogs.GetRecentAsync(parsed, skip, PageSize, ct);
            model.TotalCount = await smsLogs.CountAsync(parsed, ct);
            model.FailedCount = await smsLogs.CountAsync(EmailStatus.Failed, ct);
            model.OtherChannelFailedCount = await emailLogs.CountAsync(EmailStatus.Failed, ct);
        }
        else
        {
            model.Rows = await emailLogs.GetRecentAsync(parsed, skip, PageSize, ct);
            model.TotalCount = await emailLogs.CountAsync(parsed, ct);
            model.FailedCount = await emailLogs.CountAsync(EmailStatus.Failed, ct);
            model.OtherChannelFailedCount = await smsLogs.CountAsync(EmailStatus.Failed, ct);
        }

        return View(model);
    }
}
