using VIHouse.Entities.Experiences;

namespace VIHouse.WebUI.ViewModels.Experiences;

/// <summary>
/// Why you cannot buy this yet, and what to do about it.
///
/// This replaces the disabled grey pill that used to stand in for every non-open status. That pill
/// said the same thing about an experience opening next month and one that finished last year, and
/// it looked like a broken button in both cases.
/// </summary>
public enum ExperienceGateMode
{
    /// <summary>Announced, not yet open. Something is coming, so the indicator moves.</summary>
    Soon,

    /// <summary>Full for this round, but there is a queue to join.</summary>
    Waitlist,

    /// <summary>
    /// Closed or finished. Nothing is coming, so nothing animates — a spinner here would promise
    /// an opening that will never arrive. Quiet on purpose.
    /// </summary>
    Closed,
}

/// <summary>Where the gate is being rendered. The three CTA sites need different amounts of it.</summary>
public enum ExperienceGateVariant
{
    /// <summary>Over the cover photo. Indicator and countdown only — a form here would be heavy.</summary>
    Hero,

    /// <summary>The ticket sidebar. The full thing, including the waitlist form.</summary>
    Panel,

    /// <summary>The sticky bottom bar on phones. One line, and a link to the panel.</summary>
    Mobile,
}

public class ExperienceGateViewModel
{
    public ExperienceGateMode Mode { get; set; }
    public ExperienceGateVariant Variant { get; set; }
    public string Slug { get; set; } = default!;

    /// <summary>The headline — "Coming soon", "Waitlist", "Applications closed".</summary>
    public string LabelKey { get; set; } = default!;

    /// <summary>The line under it. Null on the mobile bar, where there is no room for one.</summary>
    public string? NoteKey { get; set; }

    /// <summary>
    /// Set only when <see cref="Experience.ApplicationOpenAt"/> is both present and still in the
    /// future. Null is the ordinary case — most experiences never have it filled in — and the gate
    /// then shows its label alone rather than an empty countdown or a date that has passed.
    /// </summary>
    public DateTimeOffset? OpensAt { get; set; }

    /// <summary>Whether to render the sign-up form. Panel variant, Waitlist mode, not already on it.</summary>
    public bool ShowWaitlistForm { get; set; }

    /// <summary>Prefilled for signed-in visitors, who should not retype what we already know.</summary>
    public string? PrefillName { get; set; }
    public string? PrefillEmail { get; set; }

    /// <summary>Their existing place in the queue, when they are already in it. Suppresses the form.</summary>
    public int? Position { get; set; }

    /// <summary>
    /// Which status this is, mapped to what the visitor can do rather than one arm per enum value —
    /// the same grouping <see cref="Helpers.ExperienceStatusFormatter.ToBadgeModifier"/> uses, and
    /// for the same reason.
    /// </summary>
    public static ExperienceGateMode ModeFor(ExperienceStatus status) => status switch
    {
        ExperienceStatus.ComingSoon => ExperienceGateMode.Soon,
        ExperienceStatus.Waitlist => ExperienceGateMode.Waitlist,
        _ => ExperienceGateMode.Closed,
    };

    public string Modifier => Mode switch
    {
        ExperienceGateMode.Soon => "soon",
        ExperienceGateMode.Waitlist => "waitlist",
        _ => "closed",
    };
}
