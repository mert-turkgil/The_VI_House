using VIHouse.Entities.Common;

namespace VIHouse.Entities.Community;

/// <summary>
/// A members-only destination outside the site — the Discord server, a broadcast channel, a
/// recurring call. Stored as data rather than hard-coded because these URLs rotate: a Discord
/// invite gets revoked and reissued, a broadcast link changes per season, and none of that should
/// need a deploy.
///
/// Never rendered to anyone without an active <see cref="Membership.Membership"/> — an invite URL
/// is a bearer credential, so a single-event ticket holder must not see it (see
/// AccountController.Community).
/// </summary>
public class CommunityLink : BaseEntity
{
    public string Label { get; set; } = default!;
    public string? Description { get; set; }
    public string Url { get; set; } = default!;

    public CommunityLinkKind Kind { get; set; } = CommunityLinkKind.Discord;

    /// <summary>Hidden from the members area without deleting it — the usual case is an invite that
    /// has been revoked and not yet replaced.</summary>
    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    // --- Who sees it -------------------------------------------------------------------------------
    // All three null: every member whose plan includes the community. Otherwise the link belongs
    // to one plan, one experience or one session, and appears on that thing's hub for the people
    // who hold it — a ticket holder sees their experience's channel without being a member.

    public Guid? MembershipPlanId { get; set; }
    public Guid? ExperienceId { get; set; }
    public Guid? SeminarId { get; set; }

    /// <summary>
    /// When set and the Discord bot is configured, the account page offers a single-use invite
    /// minted on demand for this channel instead of the static Url — so a link that leaks is
    /// worthless. Url stays as the fallback when the bot is not configured or Discord is down.
    /// </summary>
    public string? DiscordChannelId { get; set; }
}
