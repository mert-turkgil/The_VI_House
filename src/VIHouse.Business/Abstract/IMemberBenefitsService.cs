using VIHouse.Entities.Experiences;
using VIHouse.Entities.Seminars;

namespace VIHouse.Business.Abstract;

/// <summary>
/// "What does my membership get me?" — every upcoming session and experience the member's plan
/// makes free or cheaper, with where they stand on each (already in, can join now, opens later).
/// The same rules the detail pages apply (SeminarService / ExperienceService.GetAccessAsync), so
/// the list never promises something the page then refuses.
/// </summary>
public interface IMemberBenefitsService
{
    /// <returns>Null when the user holds no current membership.</returns>
    Task<MemberBenefits?> GetAsync(Guid userId, CancellationToken ct = default);
}

public enum BenefitKind
{
    /// <summary>No charge for this member: a session their plan covers, or an experience their plan is admitted to.</summary>
    Included,

    /// <summary>A member price (and any Founder extra) on a paid item.</summary>
    Discounted,
}

public record SessionBenefit(Seminar Seminar, BenefitKind Kind, SeminarAccessInfo Access, int DiscountPercent, long PriceMinor, long FullPriceMinor);

public record ExperienceBenefit(Experience Experience, BenefitKind Kind, ExperienceAccessInfo Access, int DiscountPercent,
    long? FromPriceMinor, long? FullFromPriceMinor, string? Currency);

public record MemberBenefits(
    MemberEntitlements Entitlements,
    FounderPerks Founder,
    IReadOnlyList<SessionBenefit> Sessions,
    IReadOnlyList<ExperienceBenefit> Experiences)
{
    public int IncludedCount => Sessions.Count(s => s.Kind == BenefitKind.Included) + Experiences.Count(e => e.Kind == BenefitKind.Included);
    public int DiscountedCount => Sessions.Count(s => s.Kind == BenefitKind.Discounted) + Experiences.Count(e => e.Kind == BenefitKind.Discounted);
}
