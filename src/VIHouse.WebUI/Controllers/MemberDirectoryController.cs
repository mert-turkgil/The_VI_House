using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Helpers;
using VIHouse.WebUI.ViewModels.MemberDirectory;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// The Member Directory (brief §38) — read-only, so this talks to IProfileRepository directly
/// rather than through a Business service, same reasoning as HomeController's direct use of
/// IContentPageRepository.
///
/// Two gates, and the difference between them matters. The feature flag decides whether the
/// directory is open at all — it is a later chapter of the brief, so it is off until the House
/// decides otherwise. Membership decides who may read it: not the Member role, which is granted to
/// anyone who pays for a single ticket, but an active membership. Checking the role is what let a
/// ticket-holder browse every member's profile.
///
/// Both failures are a 404 rather than a redirect to sign in. Who is in the House is not something
/// to confirm to someone who guessed the URL.
/// </summary>
[Authorize]
[Route("members")]
public class MemberDirectoryController(
    IProfileRepository profiles,
    IMembershipService membershipService,
    UserManager<ApplicationUser> userManager,
    IOptions<FeatureOptions> features) : Controller
{
    private async Task<bool> MayViewAsync(CancellationToken ct) =>
        features.Value.MemberDirectory
        && await MemberAccess.HasActiveMembershipAsync(User, membershipService, userManager, ct);

    [HttpGet("")]
    public async Task<IActionResult> Index(string? industry, string? city, string? country, string? search, CancellationToken ct)
    {
        if (!await MayViewAsync(ct)) return NotFound();

        var filter = new ProfileFilter { Industry = industry, City = city, Country = country, Search = search, Take = 50 };
        var entries = await profiles.SearchDirectoryAsync(filter, ct);

        ViewData["Title"] = "Member Directory";
        ViewData["Industry"] = industry;
        ViewData["City"] = city;
        ViewData["Country"] = country;
        ViewData["Search"] = search;

        return View(entries.Select(MemberCardViewModel.FromEntry).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        if (!await MayViewAsync(ct)) return NotFound();

        var entry = await profiles.GetDirectoryEntryAsync(id, ct);
        if (entry is null) return NotFound();

        ViewData["Title"] = $"{entry.FirstName} {entry.LastName}";
        return View(MemberDetailViewModel.FromEntry(entry));
    }
}
