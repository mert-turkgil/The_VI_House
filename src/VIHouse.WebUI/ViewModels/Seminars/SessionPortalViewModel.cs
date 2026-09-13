using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.Entities.Seminars;

namespace VIHouse.WebUI.ViewModels.Seminars;

/// <summary>
/// One enrolled session as the attendee sees it. Where <see cref="SeminarCardViewModel"/> is the
/// public card, this carries what only an attendee has: how the place was granted, what it cost,
/// and — for a live online sitting — the link to join.
/// </summary>
public class PortalSessionViewModel
{
    public string Slug { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Summary { get; set; }
    public string? HostName { get; set; }

    public bool HasCover { get; set; }
    public Guid? CoverMediaId { get; set; }

    public bool IsOnline { get; set; }
    public string? Location { get; set; }
    public DateTimeOffset? StartAtUtc { get; set; }
    public DateTimeOffset? EndAtUtc { get; set; }
    public bool IsArchived { get; set; }

    public SeminarAccessGrant GrantedVia { get; set; }
    public long AmountPaidMinor { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>The meeting link, present only for an online sitting that has one set. Never on
    /// the public card — the link is the ticket.</summary>
    public string? MeetingUrl { get; set; }

    /// <summary>True from an hour before the start until the end — when the "join" button should
    /// be the loudest thing on the page.</summary>
    public bool IsLiveNow { get; set; }

    public bool IsPaid => GrantedVia == SeminarAccessGrant.Purchase;

    public static PortalSessionViewModel FromEnrolment(SeminarEnrolment e, string? culture, DateTimeOffset now)
    {
        var s = e.Seminar;
        var copy = SeminarContent.Resolve(s, culture);
        var end = s.EndAtUtc ?? s.StartAtUtc?.AddHours(2);

        return new PortalSessionViewModel
        {
            Slug = s.Slug,
            Title = copy?.Title ?? s.Slug,
            Summary = copy?.Summary,
            HostName = s.HostName,
            HasCover = s.CoverMediaId is not null,
            CoverMediaId = s.CoverMediaId,
            IsOnline = s.IsOnline,
            Location = s.Location,
            StartAtUtc = s.StartAtUtc,
            EndAtUtc = s.EndAtUtc,
            IsArchived = s.Status == SeminarStatus.Archived,
            GrantedVia = e.Enrollment.GrantedVia,
            AmountPaidMinor = e.Enrollment.AmountMinor,
            Currency = e.Enrollment.Currency,
            ConfirmedAt = e.Enrollment.ConfirmedAt,
            MeetingUrl = s.IsOnline ? s.MeetingUrl : null,
            IsLiveNow = s.StartAtUtc is { } start && end is { } finish
                && now >= start.AddHours(-1) && now <= finish,
        };
    }
}

/// <summary>
/// The portal, in the order an attendee needs it: what is coming up (with join links), the
/// on-demand library, then what has already happened.
/// </summary>
public class SessionPortalViewModel
{
    public List<PortalSessionViewModel> Upcoming { get; set; } = [];
    public List<PortalSessionViewModel> OnDemand { get; set; } = [];
    public List<PortalSessionViewModel> Past { get; set; } = [];

    public int Total => Upcoming.Count + OnDemand.Count + Past.Count;
    public int PaidCount { get; set; }
    public int MembershipCount { get; set; }

    public static SessionPortalViewModel Build(List<SeminarEnrolment> enrolments, string? culture, DateTimeOffset now)
    {
        var items = enrolments.Select(e => PortalSessionViewModel.FromEnrolment(e, culture, now)).ToList();

        return new SessionPortalViewModel
        {
            Upcoming = items
                .Where(i => i.StartAtUtc is { } start && (i.EndAtUtc ?? start.AddHours(2)) >= now)
                .OrderBy(i => i.StartAtUtc)
                .ToList(),
            OnDemand = items.Where(i => i.StartAtUtc is null).ToList(),
            Past = items
                .Where(i => i.StartAtUtc is { } start && (i.EndAtUtc ?? start.AddHours(2)) < now)
                .OrderByDescending(i => i.StartAtUtc)
                .ToList(),
            PaidCount = items.Count(i => i.IsPaid),
            MembershipCount = items.Count(i => i.GrantedVia == SeminarAccessGrant.Membership),
        };
    }
}
