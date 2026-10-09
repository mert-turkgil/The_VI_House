using VIHouse.Entities.Common;

namespace VIHouse.Entities.Referrals;

/// <summary>
/// One place an influencer publishes — their Instagram, TikTok, YouTube channel, site — and roughly
/// how many people follow it there. Shown to admins when deciding, and in the author box under the
/// influencer's published journal posts.
/// </summary>
public class AmbassadorChannel : BaseEntity
{
    public Guid AmbassadorId { get; set; }
    public SocialPlatform Platform { get; set; }

    /// <summary>The profile address, https only.</summary>
    public string Url { get; set; } = default!;

    /// <summary>Followers / subscribers, approximately. Null when unknown.</summary>
    public int? Audience { get; set; }

    public int SortOrder { get; set; }
}

public enum SocialPlatform
{
    Instagram,
    TikTok,
    YouTube,
    X,
    LinkedIn,
    Facebook,
    Website,
    Other,
}
