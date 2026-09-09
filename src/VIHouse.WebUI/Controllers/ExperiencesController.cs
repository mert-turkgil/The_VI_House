using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Experiences;
using VIHouse.WebUI.Helpers;
using VIHouse.WebUI.ViewModels.Experiences;

namespace VIHouse.WebUI.Controllers;

[Route("experiences")]
public class ExperiencesController(
    IExperienceService experienceService,
    IMembershipService membershipService,
    IStringLocalizer<SharedResource> loc,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? city, string? status, CancellationToken ct)
    {
        // The listing gets its own description rather than inheriting the site default: a set of
        // pages sharing one description is a set Google writes its own snippet for.
        ViewData["Title"] = loc["Experiences.Title"].Value;
        this.SetSeo(loc["Seo.Experiences.Description"].Value, canonicalPath: "/experiences");
        return View(await BuildIndexAsync(city, status, ct));
    }

    /// <summary>
    /// The results region alone, for the filter enhancement in modules/filters.ts.
    ///
    /// Returns the same partial Index renders rather than JSON, so there is exactly one place that
    /// knows what a card looks like. A JSON endpoint would mean rebuilding the markup in TypeScript
    /// and keeping two renderers in step — which is how the enhanced and unenhanced views of a page
    /// start telling visitors different things.
    /// </summary>
    [HttpGet("results")]
    public async Task<IActionResult> Results(string? city, string? status, CancellationToken ct) =>
        PartialView("_ExperienceGrid", await BuildIndexAsync(city, status, ct));

    private async Task<ExperienceIndexViewModel> BuildIndexAsync(string? city, string? status, CancellationToken ct)
    {
        // Unparseable status is treated as "no filter" rather than an error: this arrives from a
        // query string, and a stale or hand-edited link should show the unfiltered page, not a 400.
        ExperienceStatus? parsedStatus = Enum.TryParse<ExperienceStatus>(status, out var s) ? s : null;

        var filter = new ExperienceFilter { City = city, Status = parsedStatus, Take = 50 };
        var experiences = await experienceService.GetPublicListingAsync(filter, ct);

        // Resolved from the cookie by UseRequestLocalization; read once rather than per card.
        var culture = CultureInfo.CurrentUICulture.Name;

        return new ExperienceIndexViewModel
        {
            Cards = [.. experiences.Select(e => ExperienceCardViewModel.FromEntity(e, culture))],
            Cities = await experienceService.GetPublicCitiesAsync(ct),
            SelectedCity = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            SelectedStatus = parsedStatus,
        };
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        var experience = await experienceService.GetPublicDetailBySlugAsync(slug, ct);
        if (experience is null || experience.Status == ExperienceStatus.Draft)
            return NotFound();

        var userId = CurrentUserId();

        // Members visibility is now honoured rather than treated as "invisible to everybody".
        // Every consumer used to test == Public, so an admin choosing Members from the dropdown
        // silently unpublished the experience — a 404 for members included.
        if (!await CanSeeAsync(experience, userId, ct))
            return NotFound();

        // Title, description, social image, hreflang set and schema.org/Event — built from the
        // experience itself rather than left to the layout's defaults. See PageSeoBuilder.
        ViewData["Seo"] = PageSeoBuilder.ForExperience(
            experience, CultureInfo.CurrentUICulture.Name, loc["Experiences.Heading"].Value);

        var model = ExperienceDetailViewModel.FromEntity(experience, CultureInfo.CurrentUICulture.Name);
        model.Access = await experienceService.GetAccessAsync(experience, userId, ct);
        model.AttendanceMode = experience.AttendanceMode;

        // Only for a waitlisted experience, and only when there is someone to look up — one indexed
        // read, so that a person already in the queue is told their number instead of being offered
        // the form they already filled in.
        if (experience.Status == ExperienceStatus.Waitlist && userId is { } signedIn)
        {
            var user = await userManager.FindByIdAsync(signedIn.ToString());
            if (user?.Email is { } email)
            {
                model.PrefillName = $"{user.FirstName} {user.LastName}".Trim();
                model.PrefillEmail = email;
                model.WaitlistPosition = await experienceService.FindWaitlistPositionAsync(experience.Id, email, ct);
            }
        }

        return View(model);
    }

    /// <summary>
    /// Takes a place in the queue for an experience that is full.
    ///
    /// Deliberately not [Authorize]: the people most worth capturing here are the ones who do not
    /// have an account yet, which is the whole reason the waitlist exists. The status is re-read
    /// inside the service, so a form rendered while this was waitlisted cannot be posted after it
    /// closed. Rate-limited with the same policy as the other anonymous public forms.
    /// </summary>
    [HttpPost("{slug}/waitlist")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("application-submit")]
    public async Task<IActionResult> JoinWaitlist(string slug, string? fullName, string? email, CancellationToken ct)
    {
        var experience = await experienceService.GetPublicDetailBySlugAsync(slug, ct);
        if (experience is null) return NotFound();

        var userId = CurrentUserId();
        if (!await CanSeeAsync(experience, userId, ct)) return NotFound();

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            TempData["StatusMessage"] = loc["Experiences.Waitlist.Incomplete"].Value;
            return RedirectToAction(nameof(Details), new { slug });
        }

        var (error, position) = await experienceService.JoinWaitlistAsync(
            experience.Id, fullName, email, userId, ct);

        // "Already on the list" arrives as an error key but is not a failure — it carries the
        // position they already hold, which is the answer they were looking for.
        TempData["StatusMessage"] = error is null
            ? loc["Experiences.Waitlist.Confirmed", position].Value
            : loc[error, position].Value;

        return RedirectToAction(nameof(Details), new { slug });
    }

    /// <summary>
    /// Takes a place on an experience the member's plan admits.
    ///
    /// One POST, and the entitlement is re-derived inside the service rather than trusted from the
    /// form — a page rendered while a plan was admitted cannot be replayed after it stops being.
    /// </summary>
    [Authorize]
    [HttpPost("{slug}/join")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("checkout")]
    public async Task<IActionResult> Join(string slug, BookingAttendance? attendance, CancellationToken ct)
    {
        var experience = await experienceService.GetPublicDetailBySlugAsync(slug, ct);
        if (experience is null) return NotFound();

        var userId = CurrentUserId();
        if (userId is null) return Challenge();

        var (error, reference) = await experienceService.JoinAsMemberAsync(experience.Id, userId.Value, attendance, ct);

        TempData["StatusMessage"] = error is null
            ? loc["Experiences.Join.Confirmed", reference!].Value
            : loc[error].Value;

        return RedirectToAction(nameof(Details), new { slug });
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(userManager.GetUserId(User), out var id) ? id : null;

    /// <summary>
    /// Whether this person may see the page at all. Mirrors SeminarService's split: visibility gates
    /// the page, entitlement gates the button on it.
    /// </summary>
    private async Task<bool> CanSeeAsync(Experience experience, Guid? userId, CancellationToken ct) =>
        experience.Visibility switch
        {
            ExperienceVisibility.Public => true,
            // Unlisted is reachable by anyone with the link; it is simply kept out of the listings,
            // which the repository already does.
            ExperienceVisibility.Unlisted => true,
            ExperienceVisibility.Members => userId is { } id
                && await membershipService.GetCurrentMembershipAsync(id, ct) is not null,
            // InviteOnly has no invitation mechanism for experiences yet, so it stays closed rather
            // than quietly behaving like Unlisted.
            _ => false,
        };
}
