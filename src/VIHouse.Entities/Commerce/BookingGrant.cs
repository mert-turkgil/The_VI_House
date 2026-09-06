namespace VIHouse.Entities.Commerce;

/// <summary>
/// Why someone holds this booking.
///
/// Worth recording separately from the amount, for the reason SeminarAccessGrant already states: a
/// zero-value booking could be a membership perk, an admin comp or a free experience, and support
/// cannot tell those apart from "AmountMinor = 0" alone.
/// </summary>
public enum BookingGrant
{
    /// <summary>Applied, was accepted, paid. The route every booking took before memberships opened.</summary>
    Purchase,

    /// <summary>An active membership on a plan this experience admits — no form, no payment.</summary>
    Membership,

    /// <summary>Given by an admin.</summary>
    Comped,
}
