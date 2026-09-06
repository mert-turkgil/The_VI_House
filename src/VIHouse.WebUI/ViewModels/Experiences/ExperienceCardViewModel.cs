using VIHouse.Business.Concrete;
using VIHouse.Entities.Experiences;
using VIHouse.WebUI.Helpers;

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

    public string? CoverImageAlt { get; set; }

    /// <summary>
    /// Where the card points. Null means the experience's own detail page, which is what almost
    /// every caller wants. The application funnel (Views/Application/ChooseExperience.cshtml) sets
    /// it to /apply?experience=… so the same card can be reused there instead of the funnel keeping
    /// its own near-identical copy of this markup, which is what it did before.
    /// </summary>
    public string? Href { get; set; }

    public int DurationDays => Math.Max(1, (EndAtUtc.Date - StartAtUtc.Date).Days + 1);

    /// <summary>Resource key, resolved by the view as @Loc[card.DurationKey, card.DurationDays].
    /// Separate singular/plural keys because "1 Days" is the kind of detail that makes a page feel
    /// machine-made, and several of the site's languages inflect it differently anyway.</summary>
    public string DurationKey => DurationDays == 1 ? "Experiences.Duration.One" : "Experiences.Duration.Many";

    public string StatusKey => Status.ToResourceKey();
    public string StatusModifier => Status.ToBadgeModifier();

    public static ExperienceCardViewModel FromEntity(Experience e, string? culture = null) => new()
    {
        Title = ExperienceContent.Title(e, culture),
        Slug = e.Slug,
        ShortSummary = ExperienceContent.ShortSummary(e, culture),
        City = e.City,
        Country = e.Country,
        StartAtUtc = e.StartAtUtc,
        EndAtUtc = e.EndAtUtc,
        // Resolved rather than copied: an uploaded cover lives in CoverImageStorageKey and is
        // streamed by MediaController, so the raw column is null for it.
        CoverImageUrl = ExperienceService.CoverUrl(e),
        CoverImageAlt = e.CoverImageAlt,
        Status = e.Status,
        FromPriceMinor = e.TicketTypes.Count > 0 ? e.TicketTypes.Min(t => t.PriceMinor) : null,
        Currency = e.TicketTypes.FirstOrDefault()?.Currency,
    };
}
