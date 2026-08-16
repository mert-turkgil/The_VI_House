using VIHouse.Entities.Experiences;

namespace VIHouse.WebUI.ViewModels.Experiences;

public class ExperienceCardViewModel
{
    public string Title { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? ShortSummary { get; set; }
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public DateTimeOffset StartAtUtc { get; set; }
    public DateTimeOffset EndAtUtc { get; set; }
    public string? CoverImageUrl { get; set; }
    public ExperienceStatus Status { get; set; }
    public long? FromPriceMinor { get; set; }
    public string? Currency { get; set; }

    public int DurationDays => Math.Max(1, (EndAtUtc.Date - StartAtUtc.Date).Days + 1);

    public string StatusLabel => Status switch
    {
        ExperienceStatus.ApplicationsOpen => "Applications Open",
        ExperienceStatus.AlmostFull => "Almost Full",
        ExperienceStatus.Waitlist => "Waitlist",
        ExperienceStatus.ApplicationsClosed => "Applications Closed",
        ExperienceStatus.Completed => "Completed",
        ExperienceStatus.ComingSoon => "Coming Soon",
        _ => "Draft",
    };

    public static ExperienceCardViewModel FromEntity(Experience e) => new()
    {
        Title = e.Title,
        Slug = e.Slug,
        ShortSummary = e.ShortSummary,
        City = e.City,
        Country = e.Country,
        StartAtUtc = e.StartAtUtc,
        EndAtUtc = e.EndAtUtc,
        CoverImageUrl = e.CoverImageUrl,
        Status = e.Status,
        FromPriceMinor = e.TicketTypes.Count > 0 ? e.TicketTypes.Min(t => t.PriceMinor) : null,
        Currency = e.TicketTypes.FirstOrDefault()?.Currency,
    };
}
