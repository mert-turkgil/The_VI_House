namespace VIHouse.Entities.Referrals;

public enum AmbassadorStatus
{
    Active,
    Inactive,

    /// <summary>Invited, not yet accepted: no account, links and QR codes do not work, the code is reserved.</summary>
    Pending,
}
