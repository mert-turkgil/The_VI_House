namespace VIHouse.Entities.Referrals;

/// <summary>
/// What a referral link pointed at. An ambassador gets one link per thing they promote — the site
/// itself (/r/{code}), one experience (/r/{code}/e/{slug}) or one session (/r/{code}/s/{slug}) —
/// and the visit remembers which, so the dashboard can say which post actually brought people.
/// </summary>
public enum ReferralTargetKind
{
    Site,
    Experience,
    Session,
}
