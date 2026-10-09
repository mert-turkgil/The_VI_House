using VIHouse.Entities.Common;

namespace VIHouse.Entities.Referrals;

/// <summary>
/// Brief §47: "Her partner/influencer unique referral (VI-ANTON) veya URL (thevihouse.com/r/anton)
/// alabilir." Shown to people as an <em>influencer</em>; the code keeps the original name. Influencers
/// are chosen by the House — there is no public way to become one.
///
/// Two ways in:
///  - Someone who already has an account is made an influencer from their user record; the row is
///    Active from the start and <see cref="UserId"/> is set.
///  - Someone new is <em>invited</em>: the admin enters everything — name, legal name, channels,
///    profile, billing and bank details, code and rate — the row is
///    <see cref="AmbassadorStatus.Pending"/> with no account behind it, and the invitation link goes
///    to <see cref="InviteEmail"/> (the admin never sees it). Following it proves the address; the
///    person only chooses a password and accepts the influencer terms. Only then does the row become
///    Active with a <see cref="UserId"/> — so a mistyped address never ends up attached to someone's
///    account, notifications or self-referral checks. The code is reserved the whole time.
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

    // --- Influencer profile ----------------------------------------------------------------------

    /// <summary>The name on their documents — copied to the account when the invitation is accepted.
    /// <see cref="Name"/> is the name they publish under.</summary>
    public string? LegalFirstName { get; set; }
    public string? LegalLastName { get; set; }

    /// <summary>A few sentences about them — shown in the author box under their published posts.</summary>
    public string? Bio { get; set; }

    /// <summary>What they make content about and for whom ("founder life, early-stage SaaS, Istanbul").</summary>
    public string? Niche { get; set; }

    /// <summary>The profile photo, in media storage (not wwwroot). Streamed by MediaController.</summary>
    public string? PhotoStorageKey { get; set; }

    public List<AmbassadorChannel> Channels { get; set; } = [];

    /// <summary>
    /// What still stands between this influencer and being fully set up. Withdrawals need the list
    /// empty: the House can only pay someone whose legal identity, address and bank are on file and
    /// who has accepted the terms. <see cref="Channels"/> must be loaded.
    /// </summary>
    public IReadOnlyList<InfluencerRequirement> MissingRequirements()
    {
        var missing = new List<InfluencerRequirement>();
        if (string.IsNullOrWhiteSpace(LegalFirstName) || string.IsNullOrWhiteSpace(LegalLastName)) missing.Add(InfluencerRequirement.LegalName);
        if (string.IsNullOrWhiteSpace(BillingAddressLine1) || string.IsNullOrWhiteSpace(BillingCity)
            || string.IsNullOrWhiteSpace(BillingPostalCode) || string.IsNullOrWhiteSpace(BillingCountry)) missing.Add(InfluencerRequirement.BillingAddress);
        if (!HasPayoutDetails) missing.Add(InfluencerRequirement.BankDetails);
        if (TermsAcceptedAt is null) missing.Add(InfluencerRequirement.Terms);
        if (!Channels.Any(c => c.Audience is > 0)) missing.Add(InfluencerRequirement.Channel);
        if (string.IsNullOrWhiteSpace(Bio)) missing.Add(InfluencerRequirement.Bio);
        if (string.IsNullOrWhiteSpace(Niche)) missing.Add(InfluencerRequirement.Niche);
        if (string.IsNullOrWhiteSpace(PhotoStorageKey)) missing.Add(InfluencerRequirement.Photo);
        return missing;
    }
}

/// <summary>One item of an influencer's profile that must be on file.</summary>
public enum InfluencerRequirement
{
    LegalName,
    BillingAddress,
    BankDetails,
    Terms,
    Channel,
    Bio,
    Niche,
    Photo,
}
