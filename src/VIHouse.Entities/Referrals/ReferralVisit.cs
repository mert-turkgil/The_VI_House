using VIHouse.Entities.Common;

namespace VIHouse.Entities.Referrals;

/// <summary>
/// One row per landing on /r/{code} — brief §47's "visits" count, plus §48's UTM attribution
/// (utm_source/utm_medium/utm_campaign/utm_content). Applications/approvals/purchases are NOT
/// duplicated here — those are derived live from Application.ReferralCode / MembershipPayment.ReferralCode
/// joined against Ambassador.Code, so there's only ever one source of truth for conversions.
/// </summary>
public class ReferralVisit : BaseEntity
{
    public Guid AmbassadorId { get; set; }

    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    public string? UtmContent { get; set; }

    /// <summary>Which link this was — the plain site link, or one scoped to an experience or a
    /// session — and the id of that experience/session when scoped. See ReferralTargetKind.</summary>
    public ReferralTargetKind TargetKind { get; set; } = ReferralTargetKind.Site;
    public Guid? TargetId { get; set; }

    /// <summary>The page the visitor was sent to, as a path — "/experiences/lisbon-2026".</summary>
    public string? LandingPath { get; set; }
}
