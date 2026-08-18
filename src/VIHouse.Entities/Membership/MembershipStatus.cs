namespace VIHouse.Entities.Membership;

// Brief §46: active / trial / past_due / cancelled / expired. PastDue/Trial exist for future
// recurring-billing support (brief explicitly frames auto-renewal as a later phase) — this MVP
// only ever sets Active or Expired.
public enum MembershipStatus
{
    Active,
    Trial,
    PastDue,
    Cancelled,
    Expired
}
