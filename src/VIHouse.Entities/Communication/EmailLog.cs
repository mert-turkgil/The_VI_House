using VIHouse.Entities.Common;

namespace VIHouse.Entities.Communication;

/// <summary>Audit trail of every transactional email attempted (brief §71). Never logs email body/PII beyond recipient+subject.</summary>
public class EmailLog : BaseEntity
{
    public string TemplateKey { get; set; } = default!;
    public string RecipientEmail { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public DateTimeOffset? SentAt { get; set; }
    public EmailStatus Status { get; set; } = EmailStatus.Queued;
    public string? ErrorMessage { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
}
