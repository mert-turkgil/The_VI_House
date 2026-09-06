using VIHouse.Entities.Common;

namespace VIHouse.Entities.Experiences;

/// <summary>
/// An experience's copy in one language.
///
/// Deliberately an *overlay* rather than the pattern Seminars and Journal posts use. Those moved
/// Title/Body out of the parent entirely; Experience keeps its English columns and this table
/// supplies de/tr/et on top. The reason is blunt: Experience.Title has twenty-odd read sites,
/// including ApplicationService and PaymentService, which put it into approval emails and booking
/// SMS. Moving the column would mean rewriting all of them to resolve a culture they have no
/// business knowing about — an email sent to one applicant has no request culture at all.
///
/// So the neutral copy lives on the Experience and is the fallback; this is what a visitor reading
/// the site in German sees instead.
/// </summary>
public class ExperienceTranslation : BaseEntity
{
    public Guid ExperienceId { get; set; }

    /// <summary>Culture name exactly as RequestLocalizationOptions supplies it, e.g. "en-GB".</summary>
    public string Culture { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string? ShortSummary { get; set; }
    public string? Description { get; set; }

    /// <summary>The room's name reads differently in some languages, and occasionally is different.</summary>
    public string? Venue { get; set; }

    /// <summary>Comma-separated, same shape as Experience.AudienceTags.</summary>
    public string? AudienceTags { get; set; }

    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? CoverImageAlt { get; set; }
}
