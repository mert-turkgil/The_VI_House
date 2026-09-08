using System.ComponentModel.DataAnnotations;
using VIHouse.Business.Options;
using VIHouse.Entities.Experiences;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// Wraps the core-fields form plus every child collection shown on the same Edit page. Each has its
/// own small add/edit/remove POST actions rather than one giant bound form — avoids the complexity
/// of dynamic-index array model binding for what is fundamentally a set of short admin lists.
///
/// The gallery and the programme were readable on the public page and editable nowhere until now;
/// they are loaded by the repository either way, and were simply being dropped on the floor here.
/// </summary>
public class AdminExperienceEditViewModel
{
    public AdminExperienceFormViewModel Form { get; set; } = default!;
    public List<TicketType> TicketTypes { get; set; } = [];
    public List<ExperienceInclusion> Inclusions { get; set; } = [];
    public List<ExperienceFaq> Faqs { get; set; } = [];
    public List<ExperienceImage> Gallery { get; set; } = [];
    public List<ExperienceProgramDay> ProgramDays { get; set; } = [];

    /// <summary>Where to show the cover from — the uploaded file, or the typed path.</summary>
    public string? CoverPreviewUrl { get; set; }

    /// <summary>Every plan that exists, with a flag for whether this experience admits it.</summary>
    public List<ExperienceMemberAccessOption> MemberAccess { get; set; } = [];

    /// <summary>
    /// Who is queued for a full experience, in arrival order. The table has existed since the first
    /// migration and nothing ever wrote to it; the public detail page now does, so there has to be
    /// somewhere to read it.
    /// </summary>
    public List<VIHouse.Entities.Commerce.WaitlistEntry> Waitlist { get; set; } = [];

    /// <summary>One tab per site language, written or not — the screen exists as much to show what
    /// is missing as to edit what is there.</summary>
    public List<ExperienceTranslationTab> Translations { get; set; } = [];

    /// <summary>Which language tab is open. Carried in the query string so a half-written German
    /// draft can be bookmarked and handed to whoever is actually writing the German.</summary>
    public string ActiveCulture { get; set; } = SiteCultures.Default;

    /// <summary>True once a cover has been uploaded. The URL box is disabled while it is, because an
    /// upload wins outright and a live-looking text field that changes nothing is worse than a
    /// greyed-out one.</summary>
    public bool HasUploadedCover { get; set; }
}

/// <param name="Admitted">Whether holders of this plan can join without applying.</param>
public record ExperienceMemberAccessOption(Guid PlanId, string Name, string PriceLabel, bool Admitted);

/// <param name="IsWritten">False when no row exists for this language yet — it falls back to English.</param>
/// <param name="IsDefault">The English copy, which lives on the experience itself and cannot be deleted.</param>
public record ExperienceTranslationTab(
    SiteCulture Culture, bool IsWritten, bool IsDefault, AdminExperienceTranslationForm Form);

/// <summary>What one language's copy looks like in the editor. Mirrors ExperienceTranslation.</summary>
public class AdminExperienceTranslationForm
{
    public Guid ExperienceId { get; set; }

    [Required, StringLength(10)]
    public string Culture { get; set; } = default!;

    [Required, StringLength(200)]
    public string Title { get; set; } = default!;

    [StringLength(400)]
    [Display(Name = "Short summary")]
    public string? ShortSummary { get; set; }

    public string? Description { get; set; }

    [StringLength(200)]
    public string? Venue { get; set; }

    [StringLength(300)]
    [Display(Name = "Who is in the room? (comma separated)")]
    public string? AudienceTags { get; set; }

    [StringLength(200)]
    [Display(Name = "SEO title")]
    public string? SeoTitle { get; set; }

    [StringLength(400)]
    [Display(Name = "SEO description")]
    public string? SeoDescription { get; set; }

    [StringLength(300)]
    [Display(Name = "Cover image description")]
    public string? CoverImageAlt { get; set; }

    /// <summary>
    /// The English tab is prefilled from the experience's own columns rather than left blank: that
    /// copy is real and already on the site, and showing an empty form beside three translated ones
    /// would read as "English is missing".
    /// </summary>
    public static AdminExperienceTranslationForm FromExperience(Experience e, string culture) => new()
    {
        ExperienceId = e.Id,
        Culture = culture,
        Title = e.Title,
        ShortSummary = e.ShortSummary,
        Description = e.Description,
        Venue = e.Venue,
        AudienceTags = e.AudienceTags,
        SeoTitle = e.SeoTitle,
        SeoDescription = e.SeoDescription,
        CoverImageAlt = e.CoverImageAlt,
    };

    public static AdminExperienceTranslationForm FromEntity(ExperienceTranslation t) => new()
    {
        ExperienceId = t.ExperienceId,
        Culture = t.Culture,
        Title = t.Title,
        ShortSummary = t.ShortSummary,
        Description = t.Description,
        Venue = t.Venue,
        AudienceTags = t.AudienceTags,
        SeoTitle = t.SeoTitle,
        SeoDescription = t.SeoDescription,
        CoverImageAlt = t.CoverImageAlt,
    };

    public static AdminExperienceTranslationForm Empty(Guid experienceId, string culture) => new()
    {
        ExperienceId = experienceId,
        Culture = culture,
        Title = "",
    };

    public ExperienceTranslation ToEntity() => new()
    {
        ExperienceId = ExperienceId,
        Culture = Culture,
        Title = Title,
        ShortSummary = ShortSummary,
        Description = Description,
        Venue = Venue,
        AudienceTags = AudienceTags,
        SeoTitle = SeoTitle,
        SeoDescription = SeoDescription,
        CoverImageAlt = CoverImageAlt,
    };
}
