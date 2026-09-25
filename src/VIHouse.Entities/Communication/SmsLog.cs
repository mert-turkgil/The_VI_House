using VIHouse.Entities.Common;

namespace VIHouse.Entities.Communication;

/// <summary>
/// Audit trail of every text message attempted, alongside <see cref="EmailLog"/> — same reason, same
/// shape. A payment link now goes out over two channels, so "did it reach them" has two answers, and
/// an applicant on the phone saying they never got their link is a question about this table as often
/// as the email one.
///
/// Stores the destination number and the template key. The body — which carries the invitation URL,
/// a single-use credential for their booking — is kept only while a failed text waits to be resent,
/// and cleared once it has been.
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

    /// <summary>The text, kept only on a Failed row that has not been resent. See EmailLog.Body.</summary>
    public string? Body { get; set; }

    /// <summary>When an admin resent this failed text; the resend is its own row.</summary>
    public DateTimeOffset? ResentAt { get; set; }
}
