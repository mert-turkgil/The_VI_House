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
}
