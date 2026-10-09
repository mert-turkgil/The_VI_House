namespace VIHouse.Business.Abstract;

/// <summary>
/// The live streams a signed-in person may watch on the site: experiences they hold a confirmed
/// booking for, and sessions they are enrolled on (membership-covered places included). Live ones
/// carry the stream URL; upcoming ones only say when — the URL is handed out only while it is open.
/// </summary>
public interface IMemberStreamService
{
    Task<List<MemberStream>> GetAsync(Guid userId, string culture, TimeSpan lookAhead, CancellationToken ct = default);
}

/// <param name="StreamUrl">Set only while the stream is open (an hour before the start to the end).</param>
public record MemberStream(string Kind, string Title, string PagePath, DateTimeOffset? StartAtUtc, DateTimeOffset? EndAtUtc, bool IsLive, string? StreamUrl);
