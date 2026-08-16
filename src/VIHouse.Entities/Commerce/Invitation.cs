using VIHouse.Entities.Common;

namespace VIHouse.Entities.Commerce;

/// <summary>
/// The unique, expiring, single-use payment link issued on application approval
/// (e.g. /invitation/AX72KL — brief §28-29). Never guessable/sequential and never reusable once
/// consumed or expired.
/// </summary>
public class Invitation : BaseEntity
{
    public string Code { get; set; } = default!;
    public Guid ApplicationId { get; set; }
    public string UserEmail { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
