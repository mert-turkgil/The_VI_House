using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Experiences;

namespace VIHouse.Business.Concrete;

public class MemberStreamService(
    IBookingRepository bookings,
    IExperienceRepository experiences,
    IRepository<ExperienceTranslation> experienceTranslations,
    ISeminarService seminarService) : IMemberStreamService
{
    public async Task<List<MemberStream>> GetAsync(Guid userId, string culture, TimeSpan lookAhead, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var horizon = now + lookAhead;
        var streams = new List<MemberStream>();

        // Experiences: confirmed bookings only — a pending payment does not open the stream.
        var confirmed = (await bookings.GetByUserAsync(userId, ct)).Where(b => b.Status == BookingStatus.Confirmed)
            .Select(b => b.ExperienceId).Distinct().ToList();
        if (confirmed.Count > 0)
        {
            var mine = await experiences.FindAsync(e => confirmed.Contains(e.Id) && e.LiveStreamUrl != null && e.EndAtUtc >= now && e.StartAtUtc <= horizon, ct);
            if (mine.Count > 0)
            {
                var ids = mine.Select(e => e.Id).ToList();
                await experienceTranslations.FindAsync(t => ids.Contains(t.ExperienceId), ct); // fixed up onto the tracked experiences
                foreach (var e in mine)
                {
                    var live = SessionTiming.IsLive(e.StartAtUtc, e.EndAtUtc, now);
                    streams.Add(new MemberStream("experience", ExperienceContent.Title(e, culture), SiteUrls.Experience(e.Slug),
                        e.StartAtUtc, e.EndAtUtc, live, live ? e.LiveStreamUrl : null));
                }
            }
        }

        // Sessions: every confirmed enrolment, however it was granted.
        foreach (var (seminar, _) in await seminarService.GetEnrolmentsForUserAsync(userId, ct))
        {
            if (string.IsNullOrWhiteSpace(seminar.LiveStreamUrl) || seminar.StartAtUtc is not { } start) continue;
            if (SessionTiming.HasEnded(start, seminar.EndAtUtc, now) || start > horizon) continue;
            var live = SessionTiming.IsLive(start, seminar.EndAtUtc, now);
            streams.Add(new MemberStream("session", SeminarContent.Title(seminar, culture), SiteUrls.Session(seminar.Slug),
                start, SessionTiming.EndOf(start, seminar.EndAtUtc), live, live ? seminar.LiveStreamUrl : null));
        }

        return streams.OrderByDescending(s => s.IsLive).ThenBy(s => s.StartAtUtc).ToList();
    }
}
