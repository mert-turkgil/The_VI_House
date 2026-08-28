using VIHouse.Entities.Experiences;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.ViewModels.Experiences;

public class ExperienceDetailViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? ShortSummary { get; set; }
    public string Description { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string? Venue { get; set; }
    public string TimeZoneId { get; set; } = default!;
    public DateTimeOffset StartAtUtc { get; set; }
    public DateTimeOffset EndAtUtc { get; set; }
    public string? CoverImageUrl { get; set; }
    public ExperienceStatus Status { get; set; }

    public List<TicketType> TicketTypes { get; set; } = [];
    public List<ExperienceProgramDay> ProgramDays { get; set; } = [];
    public List<ExperienceInclusion> Included { get; set; } = [];
    public List<ExperienceInclusion> NotIncluded { get; set; } = [];
    public List<ExperienceFaq> Faqs { get; set; } = [];
    public List<ExperienceImage> Gallery { get; set; } = [];

    public string? CoverImageAlt { get; set; }

    /// <summary>Parsed from Experience.AudienceTags. Empty when unset, and the section is then
    /// hidden — an experience that has not said who is in the room should say nothing rather than
    /// repeat a generic list, which is what the five hardcoded tags used to do on every page.</summary>
    public List<string> AudienceTags { get; set; } = [];

    public int DurationDays => Math.Max(1, (EndAtUtc.Date - StartAtUtc.Date).Days + 1);

    // Resource keys rather than English. Resolved by the view as @Loc[key, arg]; this keeps
    // IStringLocalizer out of the view model, which has no business knowing about cultures.
    public string DurationKey => DurationDays == 1 ? "Experiences.Duration.One" : "Experiences.Duration.Many";

    public string StatusKey => Status.ToResourceKey();
    public string StatusModifier => Status.ToBadgeModifier();

    public bool CanApply => Status is ExperienceStatus.ApplicationsOpen or ExperienceStatus.AlmostFull;

    public string ClosedStateKey => Status switch
    {
        ExperienceStatus.ComingSoon => "Experiences.Closed.ComingSoon",
        ExperienceStatus.Waitlist => "Experiences.Closed.Waitlist",
        _ => "Experiences.Closed.Closed",
    };

    /// <summary>
    /// Cheapest ticket across all tiers, for the mobile bar. Null when no tiers are published yet.
    /// </summary>
    public TicketType? CheapestTicket => TicketTypes.Count == 0 ? null : TicketTypes.MinBy(t => t.PriceMinor);

    public static ExperienceDetailViewModel FromEntity(Experience e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Slug = e.Slug,
        ShortSummary = e.ShortSummary,
        Description = e.Description,
        City = e.City,
        Country = e.Country,
        Venue = e.Venue,
        TimeZoneId = e.TimeZoneId,
        StartAtUtc = e.StartAtUtc,
        EndAtUtc = e.EndAtUtc,
        CoverImageUrl = e.CoverImageUrl,
        CoverImageAlt = e.CoverImageAlt,
        Status = e.Status,
        AudienceTags = string.IsNullOrWhiteSpace(e.AudienceTags)
            ? []
            : [.. e.AudienceTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
        TicketTypes = e.TicketTypes.OrderBy(t => t.SortOrder).ToList(),
        ProgramDays = e.ProgramDays.OrderBy(d => d.SortOrder).ToList(),
        Included = e.Inclusions.Where(i => i.IsIncluded).OrderBy(i => i.SortOrder).ToList(),
        NotIncluded = e.Inclusions.Where(i => !i.IsIncluded).OrderBy(i => i.SortOrder).ToList(),
        Faqs = e.Faqs.OrderBy(f => f.SortOrder).ToList(),
        Gallery = e.Gallery.OrderBy(g => g.SortOrder).ToList(),
    };
}
