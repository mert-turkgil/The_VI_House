using VIHouse.Entities.Common;

namespace VIHouse.Entities.Experiences;

/// <summary>
/// One membership plan that may join this experience without applying.
///
/// A join table rather than a flag on the experience or a tier number on the plan. Seminars use a
/// single <c>IncludedWithMembership</c> boolean, which answers "does membership cover this" but not
/// "*which* membership" — and the House sells four plans at four prices, so a flagship room that is
/// Partner-only has to be expressible. A numeric tier would force all four into one ranking, which
/// is wrong the moment two plans differ by billing period rather than seniority (Member Monthly and
/// Member).
///
/// No rows for an experience means nobody skips the form, which is the behaviour every existing
/// experience had before this table existed.
/// </summary>
public class ExperienceMembershipAccess : BaseEntity
{
    public Guid ExperienceId { get; set; }
    public Guid MembershipPlanId { get; set; }
}
