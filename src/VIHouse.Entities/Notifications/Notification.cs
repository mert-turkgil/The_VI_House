using VIHouse.Entities.Common;

namespace VIHouse.Entities.Notifications;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;

    /// <summary>Where clicking it goes — e.g. /account/bookings, /membership. Null just means "no destination, informational only."</summary>
    public string? Link { get; set; }

    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
