using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.ViewModels.MemberDirectory;

namespace VIHouse.WebUI.Controllers;

/// <summary>The Member Directory (brief §38) — read-only, so this talks to IProfileRepository
/// directly rather than through a Business service, same reasoning as HomeController's direct use
/// of IContentPageRepository. Gated to the Member role specifically: every listed profile implies
/// paid membership, and this is a private, members-only surface, not a public one.</summary>
[Authorize(Roles = Roles.Member)]
[Route("members")]
public class MemberDirectoryController(IProfileRepository profiles) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? industry, string? city, string? country, string? search, CancellationToken ct)
    {
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
        var entry = await profiles.GetDirectoryEntryAsync(id, ct);
        if (entry is null) return NotFound();

        ViewData["Title"] = $"{entry.FirstName} {entry.LastName}";
        return View(MemberDetailViewModel.FromEntry(entry));
    }
}
