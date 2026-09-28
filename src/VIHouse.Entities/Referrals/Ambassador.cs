using VIHouse.Entities.Common;

namespace VIHouse.Entities.Referrals;

/// <summary>
/// Brief §47: "Her partner/influencer unique referral (VI-ANTON) veya URL (thevihouse.com/r/anton)
/// alabilir." Ambassadors are chosen by the House — there is no public way to become one.
///
/// Two ways in:
///  - Someone who already has an account is made an ambassador from their user record; the row is
///    Active from the start and <see cref="UserId"/> is set.
///  - Someone new is <em>invited</em>: the admin types name, email, code and rate, the row is
///    <see cref="AmbassadorStatus.Pending"/> with no account behind it, and the invitation link
///    goes to <see cref="InviteEmail"/> (the admin never sees it). Following it proves the address,
///    and the person sets a password, gives their real name and payout details and accepts the
///    ambassador terms. Only then does the row become Active with a <see cref="UserId"/> — so a
///    mistyped address never ends up attached to someone's account, notifications or self-referral
///    checks. The code is reserved the whole time.
/// </summary>
public class Ambassador : BaseEntity
{
    /// <summary>The login behind the ambassador. Null while an invitation is pending.</summary>
    public Guid? UserId { get; set; }

    /// <summary>The public-facing code — used both as the /r/{code} URL segment and as a free-text value applicants/buyers can type directly.</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Whole-percent commission rate applied to attributed revenue (brief §47's "commission").</summary>
    public decimal CommissionPercent { get; set; }

    public AmbassadorStatus Status { get; set; } = AmbassadorStatus.Active;

    // --- Invitation (new people only) ------------------------------------------------------------

    /// <summary>Where the invitation was sent. Kept after acceptance as a record of it.</summary>
    public string? InviteEmail { get; set; }

    /// <summary>Language of the invitation email and page; copied to the account on acceptance.</summary>
    public string? PreferredCulture { get; set; }

    /// <summary>SHA-256 of the invitation token, hex. The token itself only ever exists in the
    /// email. Cleared on acceptance and replaced on every re-send, so only the newest link works.</summary>
    public string? InviteTokenHash { get; set; }

    public DateTimeOffset? InviteSentAt { get; set; }
    public DateTimeOffset? InviteExpiresAt { get; set; }

    /// <summary>When the ambassador finished the invitation and the links went live.</summary>
    public DateTimeOffset? ActivatedAt { get; set; }

    // --- Terms ---------------------------------------------------------------------------------

    /// <summary>Which version of the ambassador terms was accepted, when, and the ConsentRecord
    /// holding the exact text (rate, payout and refund rules) they saw.</summary>
    public string? TermsVersion { get; set; }
    public DateTimeOffset? TermsAcceptedAt { get; set; }
    public Guid? TermsConsentId { get; set; }

    /// <summary>The commission rate written into the accepted terms. If an admin later changes
    /// <see cref="CommissionPercent"/>, the difference is visible.</summary>
    public decimal? TermsCommissionPercent { get; set; }

    // --- Billing and payout ----------------------------------------------------------------------

    public string? BillingAddressLine1 { get; set; }
    public string? BillingAddressLine2 { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingPostalCode { get; set; }

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? BillingCountry { get; set; }

    /// <summary>VAT number or tax id, when they invoice as a business. Optional.</summary>
    public string? TaxId { get; set; }

    public string? PayoutAccountHolder { get; set; }

    /// <summary>Normalised: upper case, no spaces.</summary>
    public string? PayoutIban { get; set; }
    public string? PayoutBic { get; set; }
    public DateTimeOffset? PayoutDetailsUpdatedAt { get; set; }

    public bool HasPayoutDetails => !string.IsNullOrWhiteSpace(PayoutIban) && !string.IsNullOrWhiteSpace(PayoutAccountHolder);
}
