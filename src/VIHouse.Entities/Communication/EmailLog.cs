using VIHouse.Entities.Common;

namespace VIHouse.Entities.Communication;

/// <summary>Audit trail of every transactional email attempted (brief §71). A delivered email keeps
/// recipient + subject only; the body is held just while a failed one is waiting to be resent.</summary>
public class EmailLog : BaseEntity, IMessageLog
{
    public string TemplateKey { get; set; } = default!;
    public string RecipientEmail { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public DateTimeOffset? SentAt { get; set; }
    public EmailStatus Status { get; set; } = EmailStatus.Queued;
    public string? ErrorMessage { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }

    /// <summary>The rendered HTML, kept only on a Failed row that has not been resent — it is what a
    /// resend delivers, byte for byte. Cleared the moment the row is resent, and never written for a
    /// mail that went out, so a password-reset or invitation link is not stored once it has reached
    /// anyone.</summary>
    public string? Body { get; set; }

    /// <summary>When an admin resent this failed mail. The resend is its own new row; this one stays
    /// as the record of the failure but no longer counts as outstanding.</summary>
    public DateTimeOffset? ResentAt { get; set; }
}
