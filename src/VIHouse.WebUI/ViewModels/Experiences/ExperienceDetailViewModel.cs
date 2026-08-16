using VIHouse.Entities.Experiences;

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

    public int DurationDays => Math.Max(1, (EndAtUtc.Date - StartAtUtc.Date).Days + 1);

    public bool CanApply => Status is ExperienceStatus.ApplicationsOpen or ExperienceStatus.AlmostFull;

    public string ClosedStateLabel => Status switch
    {
        ExperienceStatus.ComingSoon => "Coming Soon",
        ExperienceStatus.Waitlist => "Join Waitlist",
        _ => "Applications Closed",
    };

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
        Status = e.Status,
        TicketTypes = e.TicketTypes.OrderBy(t => t.SortOrder).ToList(),
        ProgramDays = e.ProgramDays.OrderBy(d => d.SortOrder).ToList(),
        Included = e.Inclusions.Where(i => i.IsIncluded).OrderBy(i => i.SortOrder).ToList(),
        NotIncluded = e.Inclusions.Where(i => !i.IsIncluded).OrderBy(i => i.SortOrder).ToList(),
        Faqs = e.Faqs.OrderBy(f => f.SortOrder).ToList(),
        Gallery = e.Gallery.OrderBy(g => g.SortOrder).ToList(),
    };
}
