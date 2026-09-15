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
    public int MemberDiscountPercent { get; set; }
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

    /// <summary>
    /// When applications open, if that is known and has not already happened. Feeds the countdown
    /// in the gate; null is the common case, because the admin field is optional and nothing filled
    /// it in before now.
    /// </summary>
    public DateTimeOffset? ApplicationOpenAt { get; set; }

    /// <summary>Their place in the waitlist queue, when signed in and already on it. Set by the
    /// controller — it needs a database read, which a static mapper has no business doing.</summary>
    public int? WaitlistPosition { get; set; }

    public string? PrefillName { get; set; }
    public string? PrefillEmail { get; set; }

    public ExperienceGateMode GateMode => ExperienceGateViewModel.ModeFor(Status);

    /// <summary>
    /// The "you cannot buy this yet" indicator, built once per place it appears. The three CTA sites
    /// used to render three separate inline branches of the same idea; the ClosedStateKey switch
    /// this replaces produced one disabled grey pill for all of them.
    /// </summary>
    public ExperienceGateViewModel Gate(ExperienceGateVariant variant) => new()
    {
        Mode = GateMode,
        Variant = variant,
        Slug = Slug,
        LabelKey = GateMode switch
        {
            ExperienceGateMode.Soon => "Experiences.Closed.ComingSoon",
            ExperienceGateMode.Waitlist => "Experiences.Closed.Waitlist",
            _ => "Experiences.Closed.Closed",
        },
        // No note on the mobile bar: it is one line tall and a second line would push the button
        // off it.
        NoteKey = variant == ExperienceGateVariant.Mobile ? null : GateMode switch
        {
            ExperienceGateMode.Soon => "Experiences.Gate.Soon.Note",
            ExperienceGateMode.Waitlist => "Experiences.Gate.Waitlist.Note",
            _ => "Experiences.Gate.Closed.Note",
        },
        // A date in the past means the admin moved the opening and never changed the status. Show
        // nothing rather than "opens in -3 days".
        OpensAt = GateMode == ExperienceGateMode.Soon && ApplicationOpenAt > DateTimeOffset.UtcNow
            ? ApplicationOpenAt
            : null,
        ShowWaitlistForm = GateMode == ExperienceGateMode.Waitlist
            && variant == ExperienceGateVariant.Panel
            && WaitlistPosition is null,
        PrefillName = PrefillName,
        PrefillEmail = PrefillEmail,
        Position = WaitlistPosition,
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
    /// <param name="videoPlayLabel">Localised accessible name for a video's play button. Passed in
    /// because ArticleHtml lives in Business, which has no localiser and should not grow one.</param>
    public static ExperienceDetailViewModel FromEntity(Experience e, string? culture = null, string videoPlayLabel = "Play video") => new()
    {
        Id = e.Id,
        Title = ExperienceContent.Title(e, culture),
        Slug = e.Slug,
        ShortSummary = ExperienceContent.ShortSummary(e, culture),
        // EnsureHtml covers experiences written before CKEditor was wired up here, whose descriptions
        // are still plain text; RenderForDisplay turns any video marker into its click-to-play facade.
        Description = ArticleHtml.RenderForDisplay(EditorHtml.EnsureHtml(ExperienceContent.Description(e, culture)), videoPlayLabel),
        City = e.City,
        Country = e.Country,
        Venue = ExperienceContent.Venue(e, culture),
        TimeZoneId = e.TimeZoneId,
        StartAtUtc = e.StartAtUtc,
        EndAtUtc = e.EndAtUtc,
        CoverImageUrl = ExperienceService.CoverUrl(e),
        CoverImageAlt = ExperienceContent.CoverImageAlt(e, culture),
        Status = e.Status,
        ApplicationOpenAt = e.ApplicationOpenAt,
        AudienceTags = ExperienceContent.AudienceTags(e, culture) is { } tags && !string.IsNullOrWhiteSpace(tags)
            ? [.. tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
            : [],
        TicketTypes = e.TicketTypes.OrderBy(t => t.SortOrder).ToList(),
        MemberDiscountPercent = e.MemberDiscountPercent,
        ProgramDays = [.. ExperienceContent.ForCulture(e.ProgramDays, culture, d => d.Culture).OrderBy(d => d.SortOrder)],
        // The whole list in the reader's language, or the default's — never a mix.
        Included = [.. ExperienceContent.ForCulture(e.Inclusions, culture, i => i.Culture).Where(i => i.IsIncluded).OrderBy(i => i.SortOrder)],
        NotIncluded = [.. ExperienceContent.ForCulture(e.Inclusions, culture, i => i.Culture).Where(i => !i.IsIncluded).OrderBy(i => i.SortOrder)],
        Faqs = [.. ExperienceContent.ForCulture(e.Faqs, culture, f => f.Culture).OrderBy(f => f.SortOrder)],
        Gallery = e.Gallery.OrderBy(g => g.SortOrder).ToList(),
    };
}
