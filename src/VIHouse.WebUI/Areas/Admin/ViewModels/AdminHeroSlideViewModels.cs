using System.ComponentModel.DataAnnotations;
using VIHouse.Business.Options;
using VIHouse.Entities.Content;
using VIHouse.WebUI.Validation;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// The language-independent half of a hero slide: the photograph, where the buttons go, the order
/// it appears in and when it runs. Every word a visitor reads is on
/// <see cref="AdminHeroSlideTranslationFormViewModel"/> instead, one form per language — the same
/// split AdminSeminarFormViewModel makes, for the same reason.
/// </summary>
public class AdminHeroSlideFormViewModel
{
    public Guid? Id { get; set; }

    [StringLength(1000)]
    [SiteImageUrl]
    [Display(Name = "Admin.HeroSlide.ImageUrl")]
    public string? ImageUrl { get; set; }

    [StringLength(500)]
    [SiteLinkUrl]
    [Display(Name = "Admin.HeroSlide.PrimaryCtaUrl")]
    public string? PrimaryCtaUrl { get; set; }

    [StringLength(500)]
    [SiteLinkUrl]
    [Display(Name = "Admin.HeroSlide.SecondaryCtaUrl")]
    public string? SecondaryCtaUrl { get; set; }

    [Display(Name = "Admin.HeroSlide.SortOrder")]
    public int SortOrder { get; set; }

    [Display(Name = "Admin.HeroSlide.IsActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>Optional schedule, entered and displayed as UTC — the same Phase 1 simplification
    /// the experience and seminar forms make, and the reason both labels say so out loud.</summary>
    [DataType(DataType.DateTime)]
    [Display(Name = "Admin.HeroSlide.VisibleFrom")]
    public DateTime? VisibleFromUtc { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Admin.HeroSlide.VisibleUntil")]
    public DateTime? VisibleUntilUtc { get; set; }

    public static AdminHeroSlideFormViewModel FromEntity(HeroSlide s) => new()
    {
        Id = s.Id,
        ImageUrl = s.ImageUrl,
        PrimaryCtaUrl = s.PrimaryCtaUrl,
        SecondaryCtaUrl = s.SecondaryCtaUrl,
        SortOrder = s.SortOrder,
        IsActive = s.IsActive,
        VisibleFromUtc = s.VisibleFromUtc?.UtcDateTime,
        VisibleUntilUtc = s.VisibleUntilUtc?.UtcDateTime,
    };

    /// <summary>
    /// Copies the posted fields onto an existing row. Not a ToEntity(): a hero slide is never
    /// replaced wholesale, because its uploaded image and its four translations live on the tracked
    /// entity and a fresh object would drop both.
    /// </summary>
    public void ApplyTo(HeroSlide slide)
    {
        slide.ImageUrl = string.IsNullOrWhiteSpace(ImageUrl) ? null : ImageUrl.Trim();
        slide.PrimaryCtaUrl = string.IsNullOrWhiteSpace(PrimaryCtaUrl) ? null : PrimaryCtaUrl.Trim();
        slide.SecondaryCtaUrl = string.IsNullOrWhiteSpace(SecondaryCtaUrl) ? null : SecondaryCtaUrl.Trim();
        slide.SortOrder = SortOrder;
        slide.IsActive = IsActive;
        slide.VisibleFromUtc = ToUtcOffset(VisibleFromUtc);
        slide.VisibleUntilUtc = ToUtcOffset(VisibleUntilUtc);
    }

    private static DateTimeOffset? ToUtcOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}

/// <summary>One language's copy for one slide. Which language is being edited travels in
/// <see cref="Culture"/> as a hidden field, never from the query string — a stale tab must not be
/// able to save English words into the German row.</summary>
public class AdminHeroSlideTranslationFormViewModel
{
    public Guid HeroSlideId { get; set; }

    [Required, StringLength(10)]
    public string Culture { get; set; } = SiteCultures.Default;

    [StringLength(120)]
    [Display(Name = "Admin.HeroSlide.Eyebrow")]
    public string? Eyebrow { get; set; }

    [Required, StringLength(200)]
    [Display(Name = "Admin.HeroSlide.Heading")]
    public string Heading { get; set; } = default!;

    [StringLength(600)]
    [Display(Name = "Admin.HeroSlide.Subheading")]
    public string? Subheading { get; set; }

    [StringLength(60)]
    [Display(Name = "Admin.HeroSlide.PrimaryCtaLabel")]
    public string? PrimaryCtaLabel { get; set; }

    [StringLength(60)]
    [Display(Name = "Admin.HeroSlide.SecondaryCtaLabel")]
    public string? SecondaryCtaLabel { get; set; }

    [StringLength(300)]
    [Display(Name = "Admin.HeroSlide.ImageAlt")]
    public string? ImageAlt { get; set; }

    public void ApplyTo(HeroSlideTranslation t)
    {
        t.Eyebrow = Trimmed(Eyebrow);
        t.Heading = Heading.Trim();
        t.Subheading = Trimmed(Subheading);
        t.PrimaryCtaLabel = Trimmed(PrimaryCtaLabel);
        t.SecondaryCtaLabel = Trimmed(SecondaryCtaLabel);
        t.ImageAlt = Trimmed(ImageAlt);
    }

    public static AdminHeroSlideTranslationFormViewModel FromEntity(Guid slideId, HeroSlideTranslation t) => new()
    {
        HeroSlideId = slideId,
        Culture = t.Culture,
        Eyebrow = t.Eyebrow,
        Heading = t.Heading,
        Subheading = t.Subheading,
        PrimaryCtaLabel = t.PrimaryCtaLabel,
        SecondaryCtaLabel = t.SecondaryCtaLabel,
        ImageAlt = t.ImageAlt,
    };

    /// <summary>An untouched form for a language with no row yet.</summary>
    public static AdminHeroSlideTranslationFormViewModel Empty(Guid slideId, string culture) => new()
    {
        HeroSlideId = slideId,
        Culture = culture,
        Heading = string.Empty,
    };

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Everything the Edit screen shows: the slide's own fields, its image, and one tab per
/// language whether or not it has been written — the point of the screen is to show what is still
/// missing.</summary>
public class AdminHeroSlideEditViewModel
{
    public AdminHeroSlideFormViewModel Form { get; set; } = default!;

    /// <summary>What to show in the image preview: the streamed URL for an uploaded file, the
    /// pasted URL otherwise, null when the slide has no image at all.</summary>
    public string? ImagePreviewUrl { get; set; }

    /// <summary>True when the image came from an upload rather than a pasted path — the two are
    /// cleared in different ways, so the screen has to be able to tell them apart.</summary>
    public bool HasUploadedImage { get; set; }

    public List<AdminHeroSlideTranslationTab> Translations { get; set; } = [];

    /// <summary>The tab to open on load, carried through the redirect after a save so writing
    /// German copy returns to the German tab.</summary>
    public string ActiveCulture { get; set; } = SiteCultures.Default;
}

/// <param name="IsWritten">False when no row exists for this culture yet.</param>
public record AdminHeroSlideTranslationTab(
    SiteCulture Culture, bool IsWritten, bool IsDefault, AdminHeroSlideTranslationFormViewModel Form);

/// <summary>One row of the admin index.</summary>
public class AdminHeroSlideListItemViewModel
{
    public Guid Id { get; set; }
    public string Heading { get; set; } = default!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public string? ImagePreviewUrl { get; set; }
    public DateTimeOffset? VisibleFromUtc { get; set; }
    public DateTimeOffset? VisibleUntilUtc { get; set; }

    /// <summary>Which languages have copy — the fastest way to spot a slide that went live with
    /// three empty translations.</summary>
    public List<string> TranslatedCultures { get; set; } = [];

    /// <summary>False when the slide is active but its schedule puts it in the past or the future,
    /// which is the one case where "Visible" on its own would be a lie.</summary>
    public bool IsLiveNow { get; set; }
}

/// <summary>
/// The Create screen. A slide and its default-language copy are created together, because a slide
/// with no words in any language is one the homepage skips — see HeroSlideContent. The other three
/// languages get their tabs once it exists.
/// </summary>
public class AdminHeroSlideCreateViewModel
{
    public AdminHeroSlideFormViewModel Form { get; set; } = new();
    public AdminHeroSlideTranslationFormViewModel DefaultTranslation { get; set; } = new();
}
