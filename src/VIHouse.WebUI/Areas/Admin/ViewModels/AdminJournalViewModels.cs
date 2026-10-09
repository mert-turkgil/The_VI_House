using System.ComponentModel.DataAnnotations;
using VIHouse.Business.Options;
using VIHouse.Entities.Journal;
using VIHouse.WebUI.Validation;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// The language-independent fields of a journal post: which category it is in, whether it is
/// published, who wrote it, the cover. Every word a reader reads lives on
/// <see cref="AdminJournalTranslationFormViewModel"/> instead, one form per language — the same
/// split AdminSeminarFormViewModel makes, for the same reason.
/// </summary>
public class AdminJournalPostFormViewModel
{
    public Guid? Id { get; set; }

    // Not validated against a pattern any more: JournalService turns whatever is typed into a clean
    // slug (Slugs.From) and says so when it is empty or taken, instead of rejecting "My Post".
    [StringLength(200)]
    [Display(Name = "Admin.Journal.Slug")]
    public string? Slug { get; set; }

    [Required]
    [Display(Name = "Admin.Journal.Category")]
    public JournalCategory Category { get; set; }

    [Required]
    [Display(Name = "Admin.Journal.Status")]
    public JournalPostStatus Status { get; set; } = JournalPostStatus.Draft;

    [StringLength(1000)]
    [SiteImageUrl]
    [Display(Name = "Admin.Journal.CoverImageUrl")]
    public string? CoverImageUrl { get; set; }

    [StringLength(300)]
    [Display(Name = "Admin.Journal.CoverImageAlt")]
    public string? CoverImageAlt { get; set; }

    [StringLength(150)]
    [Display(Name = "Admin.Journal.AuthorName")]
    public string? AuthorName { get; set; }

    public JournalPost ToEntity() => new()
    {
        Id = Id ?? Guid.NewGuid(),
        Slug = (Slug ?? string.Empty).Trim(),
        Category = Category,
        Status = Status,
        CoverImageUrl = string.IsNullOrWhiteSpace(CoverImageUrl) ? null : CoverImageUrl.Trim(),
        CoverImageAlt = string.IsNullOrWhiteSpace(CoverImageAlt) ? null : CoverImageAlt.Trim(),
        AuthorName = string.IsNullOrWhiteSpace(AuthorName) ? null : AuthorName.Trim(),
    };

    public static AdminJournalPostFormViewModel FromEntity(JournalPost p) => new()
    {
        Id = p.Id,
        Slug = p.Slug,
        Category = p.Category,
        Status = p.Status,
        CoverImageUrl = p.CoverImageUrl,
        CoverImageAlt = p.CoverImageAlt,
        AuthorName = p.AuthorName,
    };
}

/// <summary>One language's copy. Which language is being edited travels in <see cref="Culture"/> as
/// a hidden field, never from the query string — a stale tab must not be able to save English words
/// into the German row.</summary>
public class AdminJournalTranslationFormViewModel
{
    public Guid JournalPostId { get; set; }

    [Required, StringLength(10)]
    public string Culture { get; set; } = SiteCultures.Default;

    [Required, StringLength(200)]
    [Display(Name = "Admin.Journal.Title")]
    public string Title { get; set; } = default!;

    [StringLength(500)]
    [Display(Name = "Admin.Journal.Excerpt")]
    public string? Excerpt { get; set; }

    // Per-language search copy, matching what the experience and seminar editors have always had.
    [StringLength(200)]
    [Display(Name = "Admin.Journal.SeoTitle")]
    public string? SeoTitle { get; set; }

    [StringLength(400)]
    [Display(Name = "Admin.Journal.SeoDescription")]
    public string? SeoDescription { get; set; }

    // Optional while drafting: a writer can save the headline before the article exists. Publishing
    // still needs the English body; JournalService enforces it and the publish card says so.
    [Display(Name = "Admin.Journal.Body")]
    public string? Body { get; set; }

    public JournalPostTranslation ToEntity() => new()
    {
        JournalPostId = JournalPostId,
        Culture = Culture,
        Title = Title,
        Excerpt = Excerpt,
        SeoTitle = SeoTitle,
        SeoDescription = SeoDescription,
        Body = Body ?? string.Empty,
    };

    public static AdminJournalTranslationFormViewModel FromEntity(Guid postId, JournalPostTranslation t) => new()
    {
        JournalPostId = postId,
        Culture = t.Culture,
        Title = t.Title,
        Excerpt = t.Excerpt,
        SeoTitle = t.SeoTitle,
        SeoDescription = t.SeoDescription,
        Body = t.Body,
    };

    /// <summary>An untouched form for a language that has no row yet.</summary>
    public static AdminJournalTranslationFormViewModel Empty(Guid postId, string culture) => new()
    {
        JournalPostId = postId,
        Culture = culture,
        Title = string.Empty,
        Body = string.Empty,
    };
}

/// <summary>
/// "New article": the headline and a category, and optionally the author and a standfirst. That is
/// all it takes to start; the slug comes from the title and the article is written on the next
/// screen, where images can already be uploaded.
/// </summary>
public class AdminJournalStartViewModel
{
    [Required(ErrorMessage = "Admin.Journal.Validation.TitleRequired"), StringLength(200)]
    [Display(Name = "Admin.Journal.Title")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Admin.Journal.Category")]
    public JournalCategory Category { get; set; } = JournalCategory.FounderStories;

    [StringLength(150)]
    [Display(Name = "Admin.Journal.AuthorName")]
    public string? AuthorName { get; set; }

    [StringLength(500)]
    [Display(Name = "Admin.Journal.Excerpt")]
    public string? Excerpt { get; set; }
}

/// <summary>Everything the Edit screen shows at once: the post's own fields, one tab per language,
/// and the media library. Each part posts to its own action rather than one giant bound form —
/// uploading a file must never discard a half-written German article.</summary>
public class AdminJournalEditViewModel
{
    public AdminJournalPostFormViewModel Form { get; set; } = default!;

    public List<AdminJournalTranslationTab> Translations { get; set; } = [];

    /// <summary>Attachments — the library files, cover included. Inline uploads are excluded: the
    /// article already shows them, and listing them again invites someone to "tidy up" an image
    /// that is in use.</summary>
    public List<JournalPostMedia> Media { get; set; } = [];

    public Guid? CoverMediaId { get; set; }

    /// <summary>What the cover preview should show: the streamed URL for an uploaded cover, the
    /// pasted path otherwise, null when there is none.</summary>
    public string? CoverPreviewUrl { get; set; }

    /// <summary>The culture tab to open on load, carried through the redirect after a save so
    /// writing German copy returns to the German tab.</summary>
    public string ActiveCulture { get; set; } = SiteCultures.Default;

    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Status, audience, checklist and the Publish/Update/Unpublish buttons.</summary>
    public PublishCardViewModel PublishCard { get; set; } = default!;

    /// <summary>When the post last changed on the server, in Unix milliseconds. The writer compares
    /// it with its local autosave to decide whether to offer "restore unsaved text".</summary>
    public long UpdatedAtMs { get; set; }

    /// <summary>The site's address for the post, for the slug preview and the search snippet.</summary>
    public string PublicUrlBase { get; set; } = default!;
}

/// <param name="IsWritten">False when no row exists for this culture yet.</param>
public record AdminJournalTranslationTab(
    SiteCulture Culture, bool IsWritten, bool IsDefault, AdminJournalTranslationFormViewModel Form);

/// <summary>One row of the admin index.</summary>
/// <summary>
/// One row of the media library as posted by the gallery form. The rows arrive in the order the
/// admin dragged them into: the form posts <c>items.Index</c> keyed by media id, and model binding
/// follows the posted order, so the script never renumbers field names.
/// </summary>
public class AdminJournalGalleryItemForm
{
    public Guid MediaId { get; set; }
    public bool ShowInGallery { get; set; }

    /// <summary>Culture → caption; a blank value means "none in this language".</summary>
    public Dictionary<string, string?> Captions { get; set; } = [];
}

public class AdminJournalListItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public JournalCategory Category { get; set; }
    public JournalPostStatus Status { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Which languages have copy — the fastest way to spot a post that went live with
    /// three empty translations.</summary>
    public List<string> TranslatedCultures { get; set; } = [];
}
