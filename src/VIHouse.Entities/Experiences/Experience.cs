using VIHouse.Entities.Common;

namespace VIHouse.Entities.Experiences;

/// <summary>
/// A VI House "Experience" (retreat, founder session, dinner, etc). Dates are stored in UTC;
/// TimeZoneId carries the IANA zone (e.g. "Europe/London") so the WebUI can render local time
/// without ever persisting a bare, zone-less "19:00" (brief §68).
/// </summary>
public class Experience : BaseEntity
{
    public string Slug { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? ShortSummary { get; set; }
    public string Description { get; set; } = default!;

    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string? Venue { get; set; }
    public string TimeZoneId { get; set; } = default!;

    public DateTimeOffset StartAtUtc { get; set; }
    public DateTimeOffset EndAtUtc { get; set; }

    /// <summary>Display aggregate only — TicketType.Inventory is the source of truth for sellable capacity.</summary>
    public int Capacity { get; set; }

    public ExperienceStatus Status { get; set; } = ExperienceStatus.Draft;
    public ExperienceVisibility Visibility { get; set; } = ExperienceVisibility.Public;

    /// <summary>
    /// Whether this can be attended in the room, online, or either. Defaults to in person, which is
    /// what every experience written before this field existed actually was.
    /// </summary>
    public ExperienceAttendanceMode AttendanceMode { get; set; } = ExperienceAttendanceMode.InPerson;

    /// <summary>
    /// The membership plans that may join this experience without applying. Empty — the state every
    /// existing experience is in — means everyone goes through the application form.
    /// </summary>
    public List<ExperienceMembershipAccess> MembershipAccess { get; set; } = [];

    /// <summary>
    /// A site-relative path typed into the admin, for a file committed under wwwroot at build time.
    /// Superseded by <see cref="CoverImageStorageKey"/> the moment something is uploaded.
    /// </summary>
    public string? CoverImageUrl { get; set; }

    /// <summary>
    /// Set when the cover was uploaded rather than typed. The pair works the same way as
    /// <c>HeroSlide.ImageUrl</c>/<c>ImageStorageKey</c>: an upload wins outright and nulls the URL,
    /// so there is never a question of which of the two is showing.
    ///
    /// The file lives under the media root, outside wwwroot — MapStaticAssets only serves files that
    /// existed at build time, so an uploaded cover under wwwroot would work locally and 404 in
    /// Production. It is streamed by MediaController instead.
    /// </summary>
    public string? CoverImageStorageKey { get; set; }

    /// <summary>
    /// Alt text for the cover. Almost always null, and that is correct: the cover sits directly
    /// above the heading that names the experience, so describing it again is noise to a screen
    /// reader. Set it only when the photograph carries something the surrounding copy does not.
    /// </summary>
    public string? CoverImageAlt { get; set; }

    /// <summary>
    /// Who this particular room is for — "Founders, Operators, Investors" — rendered as chips under
    /// "Who Is In The Room?". Comma-separated free text rather than its own entity, matching the
    /// convention <see cref="TicketType.PerksText"/> already sets on this aggregate: it is editorial
    /// copy, not something anything queries or joins on.
    ///
    /// Replaces five tags that were hardcoded identically into the detail view, which quietly told
    /// every visitor that every experience draws exactly the same room — on a site whose entire
    /// proposition is curation.
    /// </summary>
    public string? AudienceTags { get; set; }

    public DateTimeOffset? ApplicationOpenAt { get; set; }
    public DateTimeOffset? ApplicationCloseAt { get; set; }
    public DateTimeOffset? SalesOpenAt { get; set; }
    public DateTimeOffset? SalesCloseAt { get; set; }

    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? SeoOgImageUrl { get; set; }

    /// <summary>Whether this experience should surface in the homepage "Signature" grid.</summary>
    public bool IsSignature { get; set; }
    public int SortOrder { get; set; }

    public List<TicketType> TicketTypes { get; set; } = [];
    public List<ExperienceProgramDay> ProgramDays { get; set; } = [];
    public List<ExperienceInclusion> Inclusions { get; set; } = [];
    public List<ExperienceFaq> Faqs { get; set; } = [];
    public List<ExperienceImage> Gallery { get; set; } = [];

    /// <summary>Per-language copy overlaying the English columns above — see ExperienceTranslation.</summary>
    public List<ExperienceTranslation> Translations { get; set; } = [];
}
