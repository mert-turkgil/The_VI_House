using VIHouse.Entities.Common;

namespace VIHouse.Entities.Communication;

/// <summary>
/// Audit trail of every text message attempted, alongside <see cref="EmailLog"/> — same reason, same
/// shape. A payment link now goes out over two channels, so "did it reach them" has two answers, and
/// an applicant on the phone saying they never got their link is a question about this table as often
/// as the email one.
///
/// Stores the destination number and the template key, never the body: the body carries the
/// invitation URL, which is a single-use credential for their booking.
/// </summary>
public class SmsLog : BaseEntity
{
    public string TemplateKey { get; set; } = default!;

    /// <summary>Normalised to E.164 when it could be — otherwise whatever the applicant typed, so a
    /// number the gateway rejected is still recognisable on this screen.</summary>
    public string RecipientPhone { get; set; } = default!;

    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Shared with <see cref="EmailLog"/> deliberately: queued, sent or failed is the whole
    /// vocabulary either channel needs, and one enum keeps the admin screen's filters identical.</summary>
    public EmailStatus Status { get; set; } = EmailStatus.Queued;

    public string? ErrorMessage { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
}
