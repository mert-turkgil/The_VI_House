using VIHouse.Entities.Common;

namespace VIHouse.Entities.Commerce;

public class WaitlistEntry : BaseEntity
{
    public Guid ExperienceId { get; set; }
    public Guid? TicketTypeId { get; set; }
    public Guid? UserId { get; set; }
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public int Position { get; set; }
    public DateTimeOffset? NotifiedAt { get; set; }
}
