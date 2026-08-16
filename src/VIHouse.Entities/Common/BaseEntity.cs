namespace VIHouse.Entities.Common;

/// <summary>
/// Base type for every domain entity. IDs are GUIDs (never sequential ints) so that
/// internal database identifiers are never predictable/guessable if ever exposed.
/// Public-facing routes should use a Slug/Code/Reference instead of Id where one exists.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
