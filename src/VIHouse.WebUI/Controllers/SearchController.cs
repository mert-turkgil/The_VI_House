using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Experiences;
using VIHouse.Business.Concrete;
using VIHouse.Entities.Journal;
using VIHouse.WebUI.ViewModels.Search;

namespace VIHouse.WebUI.Controllers;

/// <summary>Public site search — Experiences (public/non-draft only) and Journal (published only).
/// Deliberately excludes anything member-gated or admin-only, same visibility rules the listing
/// pages themselves already enforce.</summary>
[Route("search")]
public class SearchController(IExperienceRepository experiences, IJournalPostRepository journalPosts) : Controller
{
    private const int MinQueryLength = 2;

    [HttpGet("results")]
    public async Task<IActionResult> Results(string? q, CancellationToken ct)
    {
        var model = new SearchResultsViewModel { Query = q ?? "", Results = await RunSearchAsync(q, take: 8, ct) };
        return PartialView("_SearchResults", model);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        ViewData["Title"] = string.IsNullOrWhiteSpace(q) ? "Search" : $"Search — {q}";
        var model = new SearchResultsViewModel { Query = q ?? "", Results = await RunSearchAsync(q, take: 50, ct) };
        return View(model);
    }

    private async Task<List<SearchResultItemViewModel>> RunSearchAsync(string? q, int take, CancellationToken ct)
    {
        var term = q?.Trim() ?? "";
        if (term.Length < MinQueryLength) return [];

        // The server's default collation is Turkish (Turkish_CI_AS), where "I"/"i" don't case-fold
        // the way they do in English — Contains(term) on the un-collated columns would silently
        // miss "izmir" against a stored "Izmir". Force a standard, non-locale-sensitive
        // case-insensitive comparison instead of relying on the database's default collation.
        const string ci = "Latin1_General_CI_AS";
        var matchedExperiences = await experiences.FindAsync(e =>
            e.Visibility == ExperienceVisibility.Public && e.Status != ExperienceStatus.Draft &&
            (EF.Functions.Collate(e.Title, ci).Contains(term) ||
             EF.Functions.Collate(e.City, ci).Contains(term) ||
             EF.Functions.Collate(e.Country, ci).Contains(term)), ct);

        // Journal copy lives one table further out (JournalPostTranslations), and the match runs
        // across every language rather than only the reader's: someone searching a German phrase
        // should find the article even while reading the site in English. The repository owns the
        // query because the collation trick above has to be applied there too.
        var matchedPosts = await journalPosts.SearchPublishedAsync(term, ct);

        var results = new List<SearchResultItemViewModel>();
        results.AddRange(matchedExperiences.Select(e => new SearchResultItemViewModel
        {
            Type = "Experience",
            Title = $"The VI House — {e.City}",
            Subtitle = $"{e.City}, {e.Country}",
            Url = $"/experiences/{e.Slug}",
        }));
        // Displayed in the reader's own language even when the match came from another one — the
        // result they click leads to a page they can read.
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        results.AddRange(matchedPosts.Select(p =>
        {
            var copy = JournalContent.Resolve(p, culture);
            return new SearchResultItemViewModel
            {
                Type = "Journal",
                Title = copy?.Title ?? p.Slug,
                Subtitle = copy?.Excerpt,
                Url = $"/journal/{p.Slug}",
            };
        }));

        return results.Take(take).ToList();
    }
}
