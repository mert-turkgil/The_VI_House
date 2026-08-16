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

    public string? CoverImageUrl { get; set; }

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
}
