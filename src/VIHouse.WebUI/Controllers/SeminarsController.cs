using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Identity;
using VIHouse.Business;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Seminars;
using VIHouse.WebUI.Helpers;
using VIHouse.WebUI.ViewModels.Seminars;

using VIHouse.WebUI.Services;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// The public face of "The VI House Sessions": browse, read the summary, sign up, and — once
/// enrolled — read the article and watch the recording.
///
/// Two rules run through every action here. The content is gated on enrolment rather than on
/// visibility alone, so the summary can sell the session while the body stays shut. And a session
/// the viewer is not entitled to even *see* is a 404, never a redirect to sign in: a members-only
/// gathering's existence is not something to confirm to whoever guesses a slug.
/// </summary>
[Route("sessions")]
public class SeminarsController(
    ISeminarService seminarService,
    ICheckoutReconciliationService reconciliation,
    IMembershipService membershipService,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var seminars = await seminarService.GetPublicListingAsync(new SeminarFilter
        {
            // Staff are included alongside members so the listing an editor sees matches the one
            // they are publishing into. Drafts stay out of it for everybody — the admin index is
            // where unfinished work belongs.
            IncludeMembersOnly = ViewerIsStaff || await ViewerIsMemberAsync(ct),
            Take = 60,
        }, ct);

        var culture = CultureInfo.CurrentUICulture.Name;
        ViewData["Title"] = loc["Seminars.Title"].Value;

        // Staff only: say what the listing is not showing visitors, so a draft or a members-only
        // session is never mistaken for one that "does not appear".
        if (ViewerIsStaff)
        {
            var hidden = await seminarService.CountHiddenFromVisitorsAsync(ct);
            if (hidden.Total > 0)
            {
                ViewData["StaffNotice"] = loc["Seminars.StaffNotice", hidden.Drafts, hidden.Restricted].Value;
                ViewData["StaffNoticeUrl"] = Url.Action("Index", "AdminSeminars", new { area = "Admin" });
            }
        }
        this.SetSeo(loc["Seo.Sessions.Description"].Value, canonicalPath: "/sessions", pageKey: "sessions");

        return View(seminars.Select(s => SeminarCardViewModel.FromEntity(s, culture)).ToList());
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        var seminar = await seminarService.GetPublicDetailBySlugAsync(slug, await ViewerIsMemberAsync(ct), ViewerIsStaff, ct);
        if (seminar is null) return NotFound();

        var access = await seminarService.GetAccessAsync(seminar, User.UserId(), ct);
        var model = SeminarDetailViewModel.FromEntity(seminar, CultureInfo.CurrentUICulture.Name, access, ViewerIsStaff);

        ViewData["Title"] = model.SeoTitle ?? model.Title;
        ViewData["Seo"] = PageSeoBuilder.ForSeminar(seminar, CultureInfo.CurrentUICulture.Name, loc["Seminars.Title"].Value);

        // Staff can open a session in any state; say when visitors cannot.
        if (ViewerIsStaff)
        {
            var banner = seminar.Status switch
            {
                SeminarStatus.Draft => loc["Preview.Seminar.Draft"].Value,
                SeminarStatus.Archived => loc["Preview.Seminar.Archived"].Value,
                _ when seminar.Visibility == SeminarVisibility.Members => loc["Preview.Seminar.Members"].Value,
                _ when seminar.Visibility == SeminarVisibility.Unlisted => loc["Preview.Seminar.Unlisted"].Value,
                _ => null,
            };
            if (banner is not null)
            {
                ViewData["PreviewBanner"] = banner;
                ViewData["PreviewBannerEdit"] = Url.Action("Edit", "AdminSeminars", new { area = "Admin", id = seminar.Id });
            }
        }

        return View(model);
    }

    /// <summary>
    /// "Add to calendar" for a live sitting. The same visibility as the page: signed out and not
    /// visible means "sign in" whether or not the slug exists, so a members-only session is never
    /// confirmed to a guesser. The meeting link is not in the file (see IcsCalendar).
    /// </summary>
    [HttpGet("{slug}/calendar.ics")]
    public async Task<IActionResult> Calendar(string slug, CancellationToken ct)
    {
        var seminar = await seminarService.GetPublicDetailBySlugAsync(slug, await ViewerIsMemberAsync(ct), ViewerIsStaff, ct);
        if (seminar is null) return User.UserId() is null ? Challenge() : NotFound();
        if (seminar.StartAtUtc is not { } start) return NotFound(); // on demand: nothing to put in a calendar

        var culture = CultureInfo.CurrentUICulture.Name;
        var pageUrl = $"{Request.Scheme}://{Request.Host}{SiteUrls.InCulture(SiteUrls.Session(seminar.Slug), culture)}";
        var ics = IcsCalendar.Build(new IcsCalendar.Event(
            $"session-{seminar.Id}@thevihouse.com",
            $"The VI House — {SeminarContent.Title(seminar, culture)}",
            start, SessionTiming.EndOf(start, seminar.EndAtUtc)!.Value,
            seminar.IsOnline ? loc["Seminars.Online"].Value : seminar.Location,
            loc["Calendar.Details", pageUrl].Value, pageUrl));

        Response.Headers["X-Robots-Tag"] = "noindex";
        return File(System.Text.Encoding.UTF8.GetBytes(ics), IcsCalendar.ContentType, IcsCalendar.FileName(seminar.Slug));
    }

    /// <summary>
    /// One button, three outcomes — free, covered by membership, or off to the payment provider.
    /// Which one is decided entirely by SeminarService from current database state; nothing about
    /// the price or the entitlement is posted from the form, so a page rendered while a session was
    /// free cannot be replayed to skip a charge.
    /// </summary>
    [Authorize]
    [HttpPost("{slug}/enrol")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("checkout")]
    public async Task<IActionResult> Enrol(string slug, CancellationToken ct)
    {
        var seminar = await seminarService.GetPublicDetailBySlugAsync(slug, await ViewerIsMemberAsync(ct), ViewerIsStaff, ct);
        if (seminar is null) return NotFound();

        var userId = User.UserId();
        if (userId is null) return Challenge();

        var access = await seminarService.GetAccessAsync(seminar, userId, ct);

        // Same attribution the apply and join forms use: whoever's link brought this visitor here.
        var referralCode = Request.Cookies[ReferralCookie.Name];

        var result = access.Outcome == SeminarAccessOutcome.RequiresPayment
            ? await seminarService.InitiateCheckoutAsync(
                seminar.Id, userId.Value,
                Url.Action(nameof(Success), "Seminars", null, Request.Scheme)! + "?session_id={CHECKOUT_SESSION_ID}",
                Url.Action(nameof(Cancelled), "Seminars", new { slug }, Request.Scheme)!,
                referralCode, ct)
            : await seminarService.EnrollAsync(seminar.Id, userId.Value, referralCode, ct);

        if (!result.Success)
        {
            // Resolved here rather than in the service: the message has to arrive in the language
            // this visitor is reading the site in, and Business has no localizer.
            TempData["SeminarError"] = loc[result.Error ?? "Seminar.Error.Unknown"].Value;
            return RedirectToAction(nameof(Details), new { slug });
        }

        if (result.Outcome == SeminarEnrollmentOutcome.RedirectToPayment)
            return Redirect(result.CheckoutUrl!);

        TempData["SeminarMessage"] = loc["Seminars.Enrolled"].Value;
        return RedirectToAction(nameof(Details), new { slug });
    }

    /// <summary>
    /// Where the payment provider sends the browser back to. Reads local state only — the redirect
    /// itself proves nothing (brief §32), so an unconfirmed enrolment renders as "processing" and
    /// waits for the webhook rather than being treated as either success or failure.
    /// </summary>
    [Authorize]
    [HttpGet("success")]
    public async Task<IActionResult> Success(string? session_id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(session_id)) return RedirectToAction(nameof(Index));

        // The provider's word on the session, through the webhook path, before local state is read.
        await reconciliation.ReconcileSessionAsync(session_id, ct);

        var info = await seminarService.GetConfirmationBySessionAsync(session_id, ct);
        if (info is null) return RedirectToAction(nameof(Index));

        ViewData["Title"] = loc["Seminars.Success.Title"].Value;
        return View(new SeminarCheckoutResultViewModel(
            info.IsConfirmed, info.SeminarTitle, info.SeminarSlug, info.AmountMinor, info.Currency)
        {
            AwaitingBank = info.AwaitingBank,
            RequiresAction = info.RequiresAction,
        });
    }

    [HttpGet("cancelled")]
    public IActionResult Cancelled(string? slug)
    {
        ViewData["Title"] = loc["Seminars.Cancelled.Title"].Value;
        ViewData["Slug"] = slug;
        return View();
    }

    /// <summary>
    /// Streams one uploaded asset, after SeminarService has decided whether this viewer may have it.
    ///
    /// AllowAnonymous is deliberate and is not a hole: the decision lives entirely in the service,
    /// which lets through the cover image (already on the public listing card) and otherwise
    /// requires a confirmed enrolment. Anonymous here only means "no automatic redirect to a login
    /// page" — an &lt;img&gt; or a &lt;video&gt; that got a login page's HTML back would simply
    /// render broken.
    ///
    /// enableRangeProcessing is what makes seeking through a recording work; without it the browser
    /// re-downloads the whole file to jump thirty seconds.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("media/{id:guid}")]
    public async Task<IActionResult> Media(Guid id, CancellationToken ct)
    {
        var file = await seminarService.OpenMediaAsync(id, User.UserId(), await ViewerIsMemberAsync(ct), ViewerIsStaff, ct);
        if (file is null) return NotFound();

        return PhysicalFile(file.PhysicalPath, file.ContentType, enableRangeProcessing: true);
    }

    // --- Viewer context ---------------------------------------------------------------------------

    /// <summary>
    /// Membership, not the Member role. The role is granted to anyone who pays — a single-ticket
    /// holder included — so checking it here let a ticket-holder into every members-only session.
    /// See MemberAccess for the distinction.
    /// </summary>
    private Task<bool> ViewerIsMemberAsync(CancellationToken ct) =>
        MemberAccess.HasActiveMembershipAsync(User, membershipService, userManager, ct);

    /// <summary>Any admin-side role. Staff read seminar media without enrolling, so an editor can
    /// preview the article they just wrote instead of seeing their own images as broken links.</summary>
    private bool ViewerIsStaff => Roles.AdminRoles.Any(User.IsInRole);
}
