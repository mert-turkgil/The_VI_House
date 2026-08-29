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

    [Required, StringLength(200)]
    [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Journal.Validation.Slug")]
    [Display(Name = "Admin.Journal.Slug")]
    public string Slug { get; set; } = default!;

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
        Slug = Slug.Trim(),
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

    [Required]
    [Display(Name = "Admin.Journal.Body")]
    public string Body { get; set; } = default!;

    public JournalPostTranslation ToEntity() => new()
    {
        JournalPostId = JournalPostId,
        Culture = Culture,
        Title = Title,
        Excerpt = Excerpt,
        Body = Body,
    };

    public static AdminJournalTranslationFormViewModel FromEntity(Guid postId, JournalPostTranslation t) => new()
    {
        JournalPostId = postId,
        Culture = t.Culture,
        Title = t.Title,
        Excerpt = t.Excerpt,
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
/// The Create screen. A post and its default-language copy are created together, because a post
/// with no title in any language is not something the site can render — the translation tabs for
/// the other three appear once it exists, along with the media library.
/// </summary>
public class AdminJournalCreateViewModel
{
    public AdminJournalPostFormViewModel Post { get; set; } = new();
    public AdminJournalTranslationFormViewModel DefaultTranslation { get; set; } = new();
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
}

/// <param name="IsWritten">False when no row exists for this culture yet.</param>
public record AdminJournalTranslationTab(
    SiteCulture Culture, bool IsWritten, bool IsDefault, AdminJournalTranslationFormViewModel Form);

/// <summary>One row of the admin index.</summary>
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
