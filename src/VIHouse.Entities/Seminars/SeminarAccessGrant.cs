namespace VIHouse.Entities.Seminars;

/// <summary>
/// Why someone has access. Worth recording separately from the amount: a zero-value enrolment could
/// be a free seminar, a membership perk or an admin comp, and support cannot tell those apart from
/// "AmountMinor = 0" alone.
/// </summary>
public enum SeminarAccessGrant
{
    /// <summary>The seminar is free to anyone who can see it.</summary>
    Free,

    /// <summary>Covered by an active membership (Seminar.IncludedWithMembership).</summary>
    Membership,

    /// <summary>Paid for individually through the payment provider.</summary>
    Purchase,

    /// <summary>Granted by an admin from the panel, outside any payment.</summary>
    Comped,
}
