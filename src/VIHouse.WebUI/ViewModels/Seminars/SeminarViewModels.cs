using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.Entities.Seminars;

namespace VIHouse.WebUI.ViewModels.Seminars;

/// <summary>
/// One session on the listing. Built from the seminar plus the reader's culture, so the card is
/// already resolved to the right language by the time the view runs — the view never reaches into
/// the translation collection itself.
/// </summary>
public class SeminarCardViewModel
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

    public long PriceMinor { get; set; }
    public string Currency { get; set; } = "GBP";
    public bool IncludedWithMembership { get; set; }

    /// <summary>Archived sessions stay readable for whoever enrolled, and are labelled as past.</summary>
    public bool IsArchived { get; set; }

    public static SeminarCardViewModel FromEntity(Seminar s, string? culture)
    {
        var copy = SeminarContent.Resolve(s, culture);

        return new SeminarCardViewModel
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
            PriceMinor = s.PriceMinor,
            Currency = s.Currency,
            IncludedWithMembership = s.IncludedWithMembership,
            IsArchived = s.Status == SeminarStatus.Archived,
        };
    }
}

/// <summary>
/// The detail page. <see cref="BodyHtml"/> and <see cref="Media"/> are populated only when
/// <see cref="Access"/> says the viewer is enrolled — the gating happens here, in the mapping, so
/// no view can leak the content by forgetting an @if.
///
/// The one exception is a staff preview (<see cref="IsStaffPreview"/>), which is what the admin
/// panel's "View page" button opens. It is kept separate from <see cref="SeminarAccessInfo"/> on
/// purpose: an editor is not enrolled, and pretending otherwise would quietly break their ability
/// to actually sign up for a session like anyone else.
/// </summary>
public class SeminarDetailViewModel
{
    public string Slug { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Summary { get; set; }

    /// <summary>Null unless the viewer has access. Sanitised on write (see EditorHtml), so it is
    /// safe to render with Html.Raw.</summary>
    public string? BodyHtml { get; set; }

    public string? HostName { get; set; }
    public string? HostTitle { get; set; }

    public bool HasCover { get; set; }
    public Guid? CoverMediaId { get; set; }

    public bool IsOnline { get; set; }
    public string? Location { get; set; }
    public string TimeZoneId { get; set; } = "Europe/London";
    public DateTimeOffset? StartAtUtc { get; set; }
    public DateTimeOffset? EndAtUtc { get; set; }

    public bool IsArchived { get; set; }
    public bool IncludedWithMembership { get; set; }

    public SeminarAccessInfo Access { get; set; } = default!;

    /// <summary>True when the content below is being shown to an editor rather than to someone who
    /// signed up. The page says so out loud, and still renders the enrolment panel underneath, so a
    /// preview shows both halves of what a real visitor would meet.</summary>
    public bool IsStaffPreview { get; set; }

    /// <summary>Gallery assets only — anything the editor inlined into the body is already in the
    /// article and would otherwise appear twice. Empty unless the viewer has access.</summary>
    public List<SeminarMedia> Media { get; set; } = [];

    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }

    public static SeminarDetailViewModel FromEntity(
        Seminar s, string? culture, SeminarAccessInfo access, bool isStaffPreview = false)
    {
        var copy = SeminarContent.Resolve(s, culture);
        var showContent = access.HasAccess || isStaffPreview;

        return new SeminarDetailViewModel
        {
            Slug = s.Slug,
            Title = copy?.Title ?? s.Slug,
            Summary = copy?.Summary,
            BodyHtml = showContent ? EditorHtml.EnsureHtml(copy?.BodyHtml ?? string.Empty) : null,
            HostName = s.HostName,
            HostTitle = s.HostTitle,
            HasCover = s.CoverMediaId is not null,
            CoverMediaId = s.CoverMediaId,
            IsOnline = s.IsOnline,
            Location = s.Location,
            TimeZoneId = s.TimeZoneId,
            StartAtUtc = s.StartAtUtc,
            EndAtUtc = s.EndAtUtc,
            IsArchived = s.Status == SeminarStatus.Archived,
            IncludedWithMembership = s.IncludedWithMembership,
            Access = access,
            Media = showContent
                ? [.. s.Media.Where(m => !m.IsInline).OrderBy(m => m.SortOrder)]
                : [],
            IsStaffPreview = isStaffPreview && !access.HasAccess,
            SeoTitle = copy?.SeoTitle,
            SeoDescription = copy?.SeoDescription,
        };
    }
}

/// <summary>The page a paid enrolment returns to. Mirrors the membership/ticket success pages:
/// local state only, and "not confirmed yet" is a processing state rather than a failure.</summary>
public record SeminarCheckoutResultViewModel(
    bool IsConfirmed, string? Title, string? Slug, long AmountMinor, string Currency);
