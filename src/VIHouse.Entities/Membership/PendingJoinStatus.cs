namespace VIHouse.Entities.Membership;

public enum PendingJoinStatus
{
    /// <summary>The form has been filled in; payment has not landed. May or may not have a live
    /// checkout session at the provider — see PendingJoin.ProviderSessionId / SessionExpiresAt.</summary>
    Pending,

    /// <summary>The provider confirmed payment and the account, profile and membership were
    /// created from this row. Terminal.</summary>
    Paid,

    /// <summary>The provider's checkout session lapsed without payment. Not terminal — the resume
    /// link opens a fresh session on the same row.</summary>
    Expired,

    /// <summary>A later join for the same address replaced this one. Not terminal either: a
    /// payment can still arrive on this row's session if it was somehow completed after the fact,
    /// and money that arrived must never be ignored.</summary>
    Superseded,
}
