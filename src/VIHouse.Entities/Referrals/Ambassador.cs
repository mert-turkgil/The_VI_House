using VIHouse.Entities.Common;

namespace VIHouse.Entities.Referrals;

/// <summary>
/// Brief §47: "Her partner/influencer unique referral (VI-ANTON) veya URL (thevihouse.com/r/anton)
/// alabilir." UserId links to a real login account (see AmbassadorController) so the partner can see
/// their own dashboard — provisioned the same way a member account is (PaymentService.ProvisionMemberAccountAsync).
/// </summary>
public class Ambassador : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>The public-facing code — used both as the /r/{code} URL segment and as a free-text value applicants/buyers can type directly.</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Whole-percent commission rate applied to attributed revenue (brief §47's "commission").</summary>
    public decimal CommissionPercent { get; set; }

    public AmbassadorStatus Status { get; set; } = AmbassadorStatus.Active;
}
