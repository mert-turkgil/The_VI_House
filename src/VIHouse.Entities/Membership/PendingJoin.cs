using VIHouse.Entities.Common;

namespace VIHouse.Entities.Membership;

/// <summary>
/// Everything the /join form collected, held until the payment provider confirms the money.
///
/// This row exists so that nothing with a uniqueness constraint — the Identity user, the profile,
/// the membership — has to be written before payment. The old flow created the ApplicationUser
/// first and then refused that email forever after an abandoned checkout ("an account already
/// exists… please sign in first", to someone who had no password to sign in with). Holding the
/// form here instead means a retry is just another row, a double-click reuses the same row, and the
/// account only comes into being in the webhook, when there is a payment to attach it to.
///
/// Also the resume mechanism: <see cref="Code"/> is an unguessable token that reopens checkout for
/// this row after the provider's session has lapsed, the same way an Invitation code reopens a
/// ticket checkout.
///
/// Deliberately thin on relationships — no navigation to ApplicationUser, because for most of its
/// life this row has no user. UserId/MembershipId are filled in on payment.
/// </summary>
public class PendingJoin : BaseEntity
{
    /// <summary>Unguessable, unique. The resume URL is /join/resume/{Code}.</summary>
    public string Code { get; set; } = default!;

    public Guid PlanId { get; set; }

    public string Email { get; set; } = default!;

    /// <summary>
    /// Trim().ToUpperInvariant() of Email. Every lookup uses this, never Email: Identity matches on
    /// its own NormalizedEmail, and a raw comparison here only agreed with that by accident of the
    /// database collation.
    /// </summary>
    public string EmailNormalized { get; set; } = default!;

    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string? City { get; set; }
    public string? JobTitle { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? PostalCode { get; set; }
    public string? About { get; set; }
    public string? Expectations { get; set; }
    public string? EarningsBand { get; set; }
    public string? ReferralCode { get; set; }

    /// <summary>A promo code redeemed for this join, so a resumed checkout carries the same
    /// discount without a second redemption.</summary>
    public Guid? PromoCodeId { get; set; }

    /// <summary>Where the terms were accepted from, for the ConsentRecord written on payment.</summary>
    public string? IpAddress { get; set; }

    /// <summary>The provider's checkout session, once one has been opened. Null between the row
    /// being saved and the provider answering — including when the provider call failed.</summary>
    public string? ProviderSessionId { get; set; }

    /// <summary>The provider's hosted checkout URL, stored so a repeat submit or a resume can send
    /// the visitor straight back without opening a second session.</summary>
    public string? CheckoutUrl { get; set; }

    /// <summary>
    /// When the provider says the session stops being payable. Read from the provider's answer,
    /// never guessed: this is what decides "still live", because the provider's expiry webhook can
    /// arrive up to an hour after the session actually lapsed.
    /// </summary>
    public DateTimeOffset? SessionExpiresAt { get; set; }

    public PendingJoinStatus Status { get; set; } = PendingJoinStatus.Pending;

    /// <summary>Filled in on payment.</summary>
    public Guid? UserId { get; set; }
    public Guid? MembershipId { get; set; }
    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>Set once the "your checkout expired — pick up where you left off" email has gone,
    /// so an abandoned form earns one email, not one per expiry.</summary>
    public DateTimeOffset? ResumeEmailSentAt { get; set; }

    /// <summary>Set by the purge sweep once the free-text and address fields have been cleared
    /// from a row that never paid — see PendingJoinPurgeService.</summary>
    public DateTimeOffset? PurgedAt { get; set; }

    public bool HasLiveSession(DateTimeOffset now) =>
        ProviderSessionId is not null && SessionExpiresAt is { } expires && expires > now;
}
