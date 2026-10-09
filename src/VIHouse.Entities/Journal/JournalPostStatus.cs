namespace VIHouse.Entities.Journal;

/// <summary>Stored as an int: new values go at the end, never in between.</summary>
public enum JournalPostStatus
{
    Draft,
    Published,

    /// <summary>An influencer sent it for review; locked for them until an admin acts.</summary>
    Submitted,

    /// <summary>An admin sent a submission back with <see cref="JournalPost.ReviewNote"/>.</summary>
    ChangesRequested,
}
