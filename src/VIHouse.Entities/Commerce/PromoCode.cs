using VIHouse.Entities.Common;

namespace VIHouse.Entities.Commerce;

public class PromoCode : BaseEntity
{
    public string Code { get; set; } = default!;
    public PromoCodeType Type { get; set; }

    /// <summary>Percentage 0-100 when Type == Percentage; minor-units amount when Type == Fixed.</summary>
    public int Value { get; set; }
    public string? Currency { get; set; }

    public Guid? ExperienceId { get; set; }
    public Guid? RestrictedToUserId { get; set; }
    public int? MaxRedemptions { get; set; }
    public int RedemptionCount { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Tickets or memberships — never both; the two checkouts price differently.</summary>
    public PromoScope Scope { get; set; } = PromoScope.Experiences;

    /// <summary>For a membership code: the one plan it applies to, or null for any.</summary>
    public Guid? MembershipPlanId { get; set; }

    /// <summary>
    /// Normalized (trimmed, upper-cased) email the code is reserved for; null means anyone. Email
    /// rather than user id because that is what an admin has in hand, and what the /join form
    /// has before an account exists. RestrictedToUserId above predates this and is unused.
    /// </summary>
    public string? RestrictedToEmail { get; set; }

    public PromoDuration MembershipDuration { get; set; } = PromoDuration.FirstPayment;

    /// <summary>The payment provider's coupon object mirroring this code, created on first use of
    /// a membership code and reused after. Null for ticket codes, which are priced locally.</summary>
    public string? ProviderCouponId { get; set; }
}
