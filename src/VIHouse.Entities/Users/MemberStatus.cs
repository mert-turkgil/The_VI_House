namespace VIHouse.Entities.Users;

/// <summary>
/// Lives on the Identity user (ApplicationUser, defined in VIHouse.DataAccess) but the enum
/// itself is a plain domain concept, so it belongs in Entities alongside everything else that
/// references member lifecycle state.
/// </summary>
public enum MemberStatus
{
    PendingApplication,
    Active,
    Suspended,
    Cancelled
}
