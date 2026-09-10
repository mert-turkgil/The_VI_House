using VIHouse.Entities.Common;

namespace VIHouse.Entities.Marketing;

/// <summary>
/// One address left on the coming-soon page while the site was still behind the curtain.
///
/// Deliberately thin. This is a list of people to tell when the doors open, not a profile: an
/// address, the language it was left in, where it came from, and whether they have been told yet.
/// Anything more would be collecting data for a purpose nobody agreed to.
///
/// Not <see cref="VIHouse.Entities.Commerce.WaitlistEntry"/>, which looks like the same thing and is
/// not: that table hangs off a required, restrict-deleted ExperienceId and carries a queue position,
/// so it cannot hold a row that is not about one specific experience.
/// </summary>
public class NotifySignup : BaseEntity
{
    /// <summary>
    /// Always stored lower case. See EfNotifySignupRepository for why that is not merely tidiness.
    /// </summary>
    public string Email { get; set; } = default!;

    /// <summary>
    /// The site language the form was submitted in, so the launch announcement can go out in the
    /// language they were reading when they asked for it.
    /// </summary>
    public string Culture { get; set; } = default!;

    /// <summary>
    /// Which capture point this came from — "coming-soon" today. Here so that a second form later
    /// (the footer newsletter, which is still inert) does not need a second table to stay
    /// distinguishable.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Set when the launch mail actually goes out. The same question WaitlistEntry.NotifiedAt
    /// answers, and the reason a second send is not a second mail to the same person.
    /// </summary>
    public DateTimeOffset? NotifiedAt { get; set; }
}
