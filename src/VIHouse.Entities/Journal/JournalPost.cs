using VIHouse.Entities.Common;

namespace VIHouse.Entities.Journal;

/// <summary>
/// Brief §125: SEO/thought-leadership editorial content — Founder Stories, Business, Technology,
/// Capital, Culture, House Notes. Admin-authored only, never user-generated; deliberately not
/// branded "Blog" anywhere in the UI (brief §126).
///
/// Every word a reader sees lives on <see cref="JournalPostTranslation"/>, one row per language the
/// admin has actually written — the same split Seminar and HeroSlide make. What stays here is the
/// things that are the same in every language: which category it is in, whether it is published,
/// who wrote it, and the photograph at the top.
/// </summary>
public class JournalPost : BaseEntity
{
    /// <summary>Shared across languages: one post, one URL. A per-language slug would fork the
    /// article's identity and split its inbound links between four addresses.</summary>
    public string Slug { get; set; } = default!;

    public JournalCategory Category { get; set; }
    public JournalPostStatus Status { get; set; } = JournalPostStatus.Draft;

    /// <summary>
    /// A site path ("/img/journal/…jpg") or an https URL, validated by SiteImageUrlAttribute on the
    /// admin form. Null on a post whose cover was uploaded through the panel instead — see
    /// <see cref="CoverMediaId"/>, which wins when both are set.
    /// </summary>
    public string? CoverImageUrl { get; set; }

    /// <summary>
    /// The uploaded cover, when there is one: a <see cref="JournalPostMedia"/> row streamed by
    /// MediaController. Uploads cannot live in wwwroot — it is served by MapStaticAssets, which
    /// only knows about files that existed at build time, so a runtime upload there works in
    /// Development and 404s in Production.
    /// </summary>
    public Guid? CoverMediaId { get; set; }

    /// <summary>Alt text for the cover. Usually null — the headline above it already carries the
    /// meaning, and a described lead image just repeats it. See Experience.CoverImageAlt.</summary>
    public string? CoverImageAlt { get; set; }

    public string? AuthorName { get; set; }

    /// <summary>Set once, the first time Status flips to Published. Drives "newest first"
    /// sort/display; a later unpublish/republish cycle does not reset it.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    public List<JournalPostTranslation> Translations { get; set; } = [];

    public List<JournalPostMedia> Media { get; set; } = [];
}
