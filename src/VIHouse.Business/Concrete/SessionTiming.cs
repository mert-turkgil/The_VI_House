namespace VIHouse.Business.Concrete;

/// <summary>
/// The one definition of when a scheduled sitting (a session, or an experience's hub card) is
/// "live" and when it is over. Every page that shows the stream, the join link or a "Live now"
/// badge asks here, so the session page, My Sessions and the account home can never disagree.
///
/// The window opens <see cref="OpensBefore"/> ahead of the start — the hour the admin hint and the
/// enrolment email promise — and closes at the end, or <see cref="DefaultLength"/> after the start
/// when no end was set. Something with no start (on demand) is never "live" and never "over".
/// </summary>
public static class SessionTiming
{
    public static readonly TimeSpan OpensBefore = TimeSpan.FromHours(1);
    public static readonly TimeSpan DefaultLength = TimeSpan.FromHours(2);

    /// <summary>The end, or the start plus <see cref="DefaultLength"/>; null when on demand.</summary>
    public static DateTimeOffset? EndOf(DateTimeOffset? startUtc, DateTimeOffset? endUtc) =>
        endUtc ?? startUtc + DefaultLength;

    public static bool IsLive(DateTimeOffset? startUtc, DateTimeOffset? endUtc, DateTimeOffset nowUtc) =>
        startUtc is { } start && nowUtc >= start - OpensBefore && nowUtc <= EndOf(start, endUtc);

    public static bool HasEnded(DateTimeOffset? startUtc, DateTimeOffset? endUtc, DateTimeOffset nowUtc) =>
        startUtc is not null && EndOf(startUtc, endUtc) < nowUtc;
}
