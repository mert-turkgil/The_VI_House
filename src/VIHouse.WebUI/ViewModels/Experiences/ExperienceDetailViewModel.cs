using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
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

    /// <summary>
    /// What this visitor can do — apply, join on their membership, or nothing. Set by the controller
    /// after FromEntity, because it needs the signed-in user and a database read that a static
    /// mapper has no business doing.
    /// </summary>
    public ExperienceAccessInfo? Access { get; set; }

    public ExperienceAttendanceMode AttendanceMode { get; set; } = ExperienceAttendanceMode.InPerson;

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

    /// <summary>
    /// <paramref name="culture"/> selects the copy. Every field falls back to the experience's own
    /// English column, so a half-written translation shows what has been translated and the original
    /// for the rest rather than blanks — see ExperienceContent.
    /// </summary>
    public static ExperienceDetailViewModel FromEntity(Experience e, string? culture = null) => new()
    {
        Id = e.Id,
        Title = ExperienceContent.Title(e, culture),
        Slug = e.Slug,
        ShortSummary = ExperienceContent.ShortSummary(e, culture),
        Description = ExperienceContent.Description(e, culture),
        City = e.City,
        Country = e.Country,
        Venue = ExperienceContent.Venue(e, culture),
        TimeZoneId = e.TimeZoneId,
        StartAtUtc = e.StartAtUtc,
        EndAtUtc = e.EndAtUtc,
        CoverImageUrl = ExperienceService.CoverUrl(e),
        CoverImageAlt = ExperienceContent.CoverImageAlt(e, culture),
        Status = e.Status,
        AudienceTags = ExperienceContent.AudienceTags(e, culture) is { } tags && !string.IsNullOrWhiteSpace(tags)
            ? [.. tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
            : [],
        TicketTypes = e.TicketTypes.OrderBy(t => t.SortOrder).ToList(),
        ProgramDays = [.. ExperienceContent.ForCulture(e.ProgramDays, culture, d => d.Culture).OrderBy(d => d.SortOrder)],
        // The whole list in the reader's language, or the default's — never a mix.
        Included = [.. ExperienceContent.ForCulture(e.Inclusions, culture, i => i.Culture).Where(i => i.IsIncluded).OrderBy(i => i.SortOrder)],
        NotIncluded = [.. ExperienceContent.ForCulture(e.Inclusions, culture, i => i.Culture).Where(i => !i.IsIncluded).OrderBy(i => i.SortOrder)],
        Faqs = [.. ExperienceContent.ForCulture(e.Faqs, culture, f => f.Culture).OrderBy(f => f.SortOrder)],
        Gallery = e.Gallery.OrderBy(g => g.SortOrder).ToList(),
    };
}
