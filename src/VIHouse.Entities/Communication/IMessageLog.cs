namespace VIHouse.Entities.Communication;

/// <summary>What the email and SMS logs have in common — enough for one repository to page,
/// count and look them up by the record they were about.</summary>
public interface IMessageLog
{
    EmailStatus Status { get; }
    string? RelatedEntityType { get; }
    Guid? RelatedEntityId { get; }
}
