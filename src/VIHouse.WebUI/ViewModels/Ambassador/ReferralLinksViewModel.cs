using VIHouse.Business;
using VIHouse.Business.Abstract;
using VIHouse.Entities.Referrals;

namespace VIHouse.WebUI.ViewModels.Ambassador;

/// <summary>
/// Everything Views/Shared/_ReferralLinks.cshtml needs to draw an ambassador's links — the plain
/// site link, one per experience, one per session — each with its copy button, QR preview and
/// downloads, and the numbers that link has produced. Rendered on the ambassador's own dashboard
/// and on the admin's ambassador page from the same partial, so the two can never disagree about
/// what a link looks like.
/// </summary>
public class ReferralLinksViewModel
{
    public required string Code { get; init; }

    /// <summary>Scheme + host the links are written against (Site:BaseUrl, or the request's).</summary>
    public required string BaseUrl { get; init; }

    public List<ReferralLinkTarget> Targets { get; init; } = [];
    public List<ReferralTargetStats> Stats { get; init; } = [];

    /// <summary>"admin" on the panel (admin-btn classes), "site" on the member-facing dashboard.</summary>
    public string Style { get; init; } = "site";

    public string SiteLink => SiteUrls.Absolute(BaseUrl, SiteUrls.Referral(Code));

    public string LinkFor(ReferralLinkTarget target) => SiteUrls.Absolute(BaseUrl, target.Kind == ReferralTargetKind.Experience
        ? SiteUrls.ReferralExperience(Code, target.Slug)
        : SiteUrls.ReferralSession(Code, target.Slug));

    /// <summary>The "to" value ReferralController's qr endpoints take: "e:{slug}" / "s:{slug}".</summary>
    public static string QrTarget(ReferralLinkTarget target) =>
        $"{(target.Kind == ReferralTargetKind.Experience ? "e" : "s")}:{target.Slug}";

    public string QrUrl(string format, ReferralLinkTarget? target = null) =>
        $"{SiteUrls.Referral(Code)}/qr.{format}{(target is null ? "" : "?to=" + Uri.EscapeDataString(QrTarget(target)))}";

    public ReferralTargetStats? StatsFor(ReferralTargetKind kind, Guid? id) =>
        Stats.FirstOrDefault(s => s.Kind == kind && s.Id == id);

    public IEnumerable<ReferralLinkTarget> Experiences => Targets.Where(t => t.Kind == ReferralTargetKind.Experience);
    public IEnumerable<ReferralLinkTarget> Sessions => Targets.Where(t => t.Kind == ReferralTargetKind.Session);
}
