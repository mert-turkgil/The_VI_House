namespace VIHouse.Entities.Referrals;

/// <summary>The steps of a referral that are worth telling the ambassador about, in order.</summary>
public enum ReferralConversionKind
{
    /// <summary>Someone who arrived through the link applied to an experience.</summary>
    Application,
    /// <summary>That application was approved — the invitation is out.</summary>
    Approved,
    /// <summary>A ticket was paid for.</summary>
    TicketPurchase,
    /// <summary>A membership was paid for.</summary>
    MembershipPurchase,
}
