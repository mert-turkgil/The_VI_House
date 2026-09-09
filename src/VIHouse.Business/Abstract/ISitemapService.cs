namespace VIHouse.Business.Abstract;

/// <summary>
/// The public surface of the site, as data — one list that the sitemap, llms.txt and any future
/// crawler endpoint all read.
///
/// Kept in Business rather than assembled in a controller because getting it wrong is silent: a
/// sitemap that lists a page which 404s, or omits half the journal, produces no error anywhere and
/// is only noticed weeks later in Search Console.
/// </summary>
public interface ISitemapService
{
    Task<IReadOnlyList<SitemapEntry>> GetEntriesAsync(CancellationToken ct = default);
}

/// <param name="Path">
/// Language-free and site-relative, e.g. "/experiences/zurich-2026". The per-language URLs are
/// built from it, so this must never already carry a language prefix.
/// </param>
/// <param name="Priority">0.0-1.0. A hint about relative importance within this site only.</param>
/// <param name="Cultures">
/// The languages this page genuinely exists in. Everything static exists in all four; a journal
/// post exists in the languages someone actually translated it into. Declaring a translation that
/// is not there is worse than declaring none.
/// </param>
public record SitemapEntry(
    string Path,
    DateTimeOffset? LastModified,
    string ChangeFrequency,
    double Priority,
    IReadOnlyList<string> Cultures)
{
    /// <summary>Title and summary in the default language, for llms.txt. Not used by the sitemap.</summary>
    public string? Title { get; init; }
    public string? Summary { get; init; }

    /// <summary>Groups the entry under a heading in llms.txt — "Experiences", "Journal".</summary>
    public string Section { get; init; } = "Pages";
}
