namespace VIHouse.Entities.Applications;

/// <summary>
/// Valid transitions are enforced in VIHouse.Business's ApplicationService, not here — this enum
/// only names the states. See ApplicationService for the transition table (brief §27).
/// </summary>
public enum ApplicationStatus
{
    Draft,
    Submitted,
    UnderReview,
    Shortlisted,
    Approved,
    Rejected,
    Waitlisted,
    PaymentPending,
    Paid,
    Cancelled
}
