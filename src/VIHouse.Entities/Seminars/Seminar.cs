using VIHouse.Entities.Common;

namespace VIHouse.Entities.Seminars;

/// <summary>
/// "The VI House Sessions" (brief §6) — the small, mostly-online end of the programme: workshops,
/// founder sessions, seminars and member resources, as opposed to the multi-day, ticket-tiered,
/// application-gated <see cref="Experiences.Experience"/>.
///
/// The split is deliberate rather than cosmetic. An Experience sells through an application, an
/// invitation code and per-tier inventory; a Seminar is simply signed up for, is free to anyone
/// whose membership covers it, and carries a body of authored content (video, stills, written
/// material) that outlives the date it was held on. Folding one into the other would have meant
/// bolting an approval funnel onto a video page.
///
/// Nothing on this type is localised: every piece of reader-facing copy lives on
/// <see cref="SeminarTranslation"/>, one row per culture, so adding a language never means adding
/// a column. Money is integer minor units (brief §184), never decimal.
/// </summary>
public class Seminar : BaseEntity
{
    /// <summary>Public-facing route key — /sessions/{slug}. Stable across translations, so a link
    /// shared in one language still resolves in another.</summary>
    public string Slug { get; set; } = default!;

    public SeminarStatus Status { get; set; } = SeminarStatus.Draft;

    public SeminarVisibility Visibility { get; set; } = SeminarVisibility.Members;

    /// <summary>Who is running it — shown on the card and the detail page.</summary>
    public string? HostName { get; set; }
    public string? HostTitle { get; set; }

    /// <summary>
    /// The image used on the listing card and at the top of the detail page, chosen from this
    /// seminar's own media. A row id rather than a URL so the cover is subject to the same storage,
    /// validation and cleanup as everything else — and so "delete this file" cannot leave a card
    /// pointing at nothing. Served publicly (to whoever may see the seminar at all) via
    /// /sessions/{slug}/cover, unlike the rest of the media, which is behind enrolment.
    /// </summary>
    public Guid? CoverMediaId { get; set; }

    /// <summary>Most sessions are online; a false value turns <see cref="Location"/> into the venue.</summary>
    public bool IsOnline { get; set; } = true;
    public string? Location { get; set; }

    /// <summary>IANA zone (e.g. "Europe/London"), so a start time is never persisted as a bare,
    /// zone-less "19:00" — same rule as Experience (brief §68).</summary>
    public string TimeZoneId { get; set; } = "Europe/London";

    /// <summary>Null for an on-demand session — recorded content with no sitting to attend.</summary>
    public DateTimeOffset? StartAtUtc { get; set; }
    public DateTimeOffset? EndAtUtc { get; set; }

    /// <summary>Seats available for a live sitting. Zero means unlimited, which is the normal case
    /// for on-demand content.</summary>
    public int Capacity { get; set; }

    /// <summary>Integer minor units (pence/cents). Zero means free to anyone who can see it.</summary>
    public long PriceMinor { get; set; }
    public string Currency { get; set; } = "GBP";

    /// <summary>
    /// When true, an active membership already pays for this — the member enrols in one click and
    /// is never sent to Stripe. Everyone else pays <see cref="PriceMinor"/>. This is the whole
    /// "free if you are subscribed, priced if you are not" rule, and it is per-seminar because a
    /// flagship session may well sit outside what the membership covers.
    /// </summary>
    public bool IncludedWithMembership { get; set; } = true;

    /// <summary>Set once, the first time Status flips to Published; a later unpublish/republish
    /// cycle does not reset it. Drives "newest first" ordering — same rule as JournalPost.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    public int SortOrder { get; set; }

    public List<SeminarTranslation> Translations { get; set; } = [];
    public List<SeminarMedia> Media { get; set; } = [];
}
