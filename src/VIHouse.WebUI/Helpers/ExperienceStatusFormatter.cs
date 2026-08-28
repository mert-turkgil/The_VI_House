using VIHouse.Entities.Experiences;

namespace VIHouse.WebUI.Helpers;

public static class ExperienceStatusFormatter
{
    /// <summary>
    /// The resource key for a status, not the English words.
    ///
    /// View models used to return "Applications Open" directly, which meant the one thing a visitor
    /// looks at first on a card stayed English on a site with a four-language switcher. Returning a
    /// key lets the view do <c>@Loc[card.StatusKey]</c> and keeps IStringLocalizer out of the view
    /// models, where it does not belong.
    /// </summary>
    public static string ToResourceKey(this ExperienceStatus status) =>
        $"Experiences.Status.{status}";

    /// <summary>
    /// BEM modifier for the status badge, so the colour mapping lives next to the label mapping
    /// rather than as a switch expression inlined in Razor.
    ///
    /// Grouped by what the visitor should *do*, not one-per-enum: open and almost-full are both
    /// "you can still apply", waitlist and coming-soon are both "not yet", closed and completed are
    /// both "not any more". Six values, three treatments.
    /// </summary>
    public static string ToBadgeModifier(this ExperienceStatus status) => status switch
    {
        ExperienceStatus.ApplicationsOpen => "open",
        ExperienceStatus.AlmostFull => "almost-full",
        ExperienceStatus.Waitlist => "waitlist",
        ExperienceStatus.ComingSoon => "soon",
        _ => "closed",
    };

    /// <summary>
    /// English fallback, kept only for the admin panel, which is not localised and reads these
    /// straight. Public views must use <see cref="ToResourceKey"/>.
    /// </summary>
    public static string ToDisplayLabel(this ExperienceStatus status) => status switch
    {
        ExperienceStatus.ApplicationsOpen => "Applications Open",
        ExperienceStatus.AlmostFull => "Almost Full",
        ExperienceStatus.ApplicationsClosed => "Applications Closed",
        ExperienceStatus.ComingSoon => "Coming Soon",
        _ => status.ToString(),
    };
}
