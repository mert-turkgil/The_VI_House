namespace VIHouse.Entities.Notifications;

// Brief §73. "Connection request" is deliberately excluded — it depends on the Connection System,
// which the brief itself marks as not required for MVP (§39).
public enum NotificationType
{
    ApplicationApproved,
    Payment,
    EventUpdate,
    NewSchedule
}
