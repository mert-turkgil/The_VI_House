using System.ComponentModel.DataAnnotations;
using VIHouse.Entities.Experiences;
using VIHouse.WebUI.Validation;
using VIHouse.Business.Concrete;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// Backs both Create and Edit for an Experience's core (non-collection) fields. Start/End are
/// bound as plain DateTime from <input type="datetime-local"> and treated as UTC directly — a
/// Phase 1 simplification; a timezone-aware picker keyed off TimeZoneId is a fast-follow, not a
/// blocker for admin usability.
/// </summary>
public class AdminExperienceFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(200)]
    [Display(Name = "Admin.Field.Title")]
    public string Title { get; set; } = default!;

    [Required, StringLength(200)]
    [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Admin.Validation.LowercaseLettersNumbersAndHyphensOnly")]
    [Display(Name = "Admin.Field.Slug")]
    public string Slug { get; set; } = default!;

    [StringLength(400)]
    [Display(Name = "Admin.Field.ShortSummary")]
    public string? ShortSummary { get; set; }

    [Required]
    [Display(Name = "Admin.Field.Description")]
    public string Description { get; set; } = default!;

    [Required, StringLength(100)]
    [Display(Name = "Admin.Field.City")]
    public string City { get; set; } = default!;

    [Required, StringLength(2)]
    [Display(Name = "Admin.Field.Country")]
    public string Country { get; set; } = default!;

    [StringLength(200)]
    [Display(Name = "Admin.Field.Venue")]
    public string? Venue { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Admin.Field.TimeZone")]
    public string TimeZoneId { get; set; } = "Europe/London";

    [Required]
    [Display(Name = "Admin.Field.StartUTC")]
    [DataType(DataType.DateTime)]
    public DateTime StartAtUtc { get; set; }

    [Required]
    [Display(Name = "Admin.Field.EndUTC")]
    [DataType(DataType.DateTime)]
    public DateTime EndAtUtc { get; set; }

    [Range(1, 1000)]
    [Display(Name = "Admin.Field.Capacity")]
    public int Capacity { get; set; } = 20;

    [Display(Name = "Admin.Field.Status")]
    public ExperienceStatus Status { get; set; } = ExperienceStatus.Draft;
    [Display(Name = "Admin.Field.Visibility")]
    public ExperienceVisibility Visibility { get; set; } = ExperienceVisibility.Public;

    [Display(Name = "Admin.Field.HowCanItBeAttended")]
    public ExperienceAttendanceMode AttendanceMode { get; set; } = ExperienceAttendanceMode.InPerson;

    /// <summary>Online / Both only: the room ticket holders join, shown on their account hub.</summary>
    [Url, StringLength(500)]
    [Display(Name = "Admin.Field.MeetingLink")]
    public string? MeetingUrl { get; set; }

    /// <summary>Online / Both only: a YouTube live URL embedded on the holder's hub near start time.</summary>
    [Url, StringLength(500)]
    [Display(Name = "Admin.Field.LiveStreamYouTube")]
    public string? LiveStreamUrl { get; set; }

    [StringLength(1000)]
    [SiteImageUrl]
    [Display(Name = "Admin.Field.CoverImageURL")]
    public string? CoverImageUrl { get; set; }

    /// <summary>
    /// Usually left empty, and that is right: the cover sits directly above the heading that names
    /// the experience, so describing it again is noise to a screen reader. Fill it only when the
    /// photograph carries something the surrounding copy does not.
    /// </summary>
    [StringLength(300)]
    [Display(Name = "Admin.Field.CoverImageDescription")]
    public string? CoverImageAlt { get; set; }

    /// <summary>Comma-separated, rendered as the chips under "Who is in the room?".</summary>
    [StringLength(300)]
    [Display(Name = "Admin.Field.WhoIsInTheRoomComma")]
    public string? AudienceTags { get; set; }

    [Display(Name = "Admin.Field.ApplicationOpens")]
    public DateTime? ApplicationOpenAt { get; set; }

    [Display(Name = "Admin.Field.ApplicationCloses")]
    public DateTime? ApplicationCloseAt { get; set; }

    [Display(Name = "Admin.Field.SalesOpen")]
    public DateTime? SalesOpenAt { get; set; }

    [Display(Name = "Admin.Field.SalesClose")]
    public DateTime? SalesCloseAt { get; set; }

    [StringLength(200)]
    [Display(Name = "Admin.Field.SEOTitle2")]
    public string? SeoTitle { get; set; }

    [StringLength(400)]
    [Display(Name = "Admin.Field.SEODescription2")]
    public string? SeoDescription { get; set; }

    /// <summary>The image a link to this experience shows when pasted into a chat or a social post.
    /// Falls back to the cover when empty.</summary>
    [StringLength(1000)]
    [SiteImageUrl]
    [Display(Name = "Admin.Field.SocialPreviewImageURL")]
    public string? SeoOgImageUrl { get; set; }

    [Display(Name = "Admin.Field.ShowInHomepageSignatureGrid")]
    public bool IsSignature { get; set; }

    [Range(0, 100)]
    [Display(Name = "Admin.Field.MemberDiscount", Description = "Percentage off every ticket for current members. 0 means members pay full price. Who may attend is set by Visibility and the admitted plans, not here.")]
    public int MemberDiscountPercent { get; set; }

    [Display(Name = "Members can join from (UTC)")]
    public DateTime? MembersOpenAtUtc { get; set; }

    public int SortOrder { get; set; }

    public Experience ToEntity() => new()
    {
        Id = Id ?? Guid.NewGuid(),
        Title = Title,
        Slug = Slug,
        ShortSummary = ShortSummary,
        Description = Description,
        City = City,
        Country = Country,
        Venue = Venue,
        TimeZoneId = TimeZoneId,
        StartAtUtc = new DateTimeOffset(DateTime.SpecifyKind(StartAtUtc, DateTimeKind.Utc)),
        EndAtUtc = new DateTimeOffset(DateTime.SpecifyKind(EndAtUtc, DateTimeKind.Utc)),
        Capacity = Capacity,
        Status = Status,
        Visibility = Visibility,
        AttendanceMode = AttendanceMode,
        MeetingUrl = string.IsNullOrWhiteSpace(MeetingUrl) ? null : MeetingUrl.Trim(),
        LiveStreamUrl = string.IsNullOrWhiteSpace(LiveStreamUrl) ? null : LiveStreamUrl.Trim(),
        CoverImageUrl = CoverImageUrl,
        CoverImageAlt = CoverImageAlt,
        AudienceTags = AudienceTags,
        ApplicationOpenAt = UtcDates.ToOffset(ApplicationOpenAt),
        ApplicationCloseAt = UtcDates.ToOffset(ApplicationCloseAt),
        SalesOpenAt = UtcDates.ToOffset(SalesOpenAt),
        SalesCloseAt = UtcDates.ToOffset(SalesCloseAt),
        SeoTitle = SeoTitle,
        SeoDescription = SeoDescription,
        SeoOgImageUrl = SeoOgImageUrl,
        IsSignature = IsSignature,
        MemberDiscountPercent = MemberDiscountPercent,
        MembersOpenAtUtc = UtcDates.ToOffset(MembersOpenAtUtc),
        SortOrder = SortOrder,
    };

    public static AdminExperienceFormViewModel FromEntity(Experience e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Slug = e.Slug,
        ShortSummary = e.ShortSummary,
        Description = e.Description,
        City = e.City,
        Country = e.Country,
        Venue = e.Venue,
        TimeZoneId = e.TimeZoneId,
        StartAtUtc = e.StartAtUtc.UtcDateTime,
        EndAtUtc = e.EndAtUtc.UtcDateTime,
        Capacity = e.Capacity,
        Status = e.Status,
        Visibility = e.Visibility,
        AttendanceMode = e.AttendanceMode,
        MeetingUrl = e.MeetingUrl,
        LiveStreamUrl = e.LiveStreamUrl,
        CoverImageUrl = e.CoverImageUrl,
        CoverImageAlt = e.CoverImageAlt,
        AudienceTags = e.AudienceTags,
        ApplicationOpenAt = e.ApplicationOpenAt?.UtcDateTime,
        ApplicationCloseAt = e.ApplicationCloseAt?.UtcDateTime,
        SalesOpenAt = e.SalesOpenAt?.UtcDateTime,
        SalesCloseAt = e.SalesCloseAt?.UtcDateTime,
        SeoTitle = e.SeoTitle,
        SeoDescription = e.SeoDescription,
        SeoOgImageUrl = e.SeoOgImageUrl,
        IsSignature = e.IsSignature,
        MemberDiscountPercent = e.MemberDiscountPercent,
        MembersOpenAtUtc = e.MembersOpenAtUtc?.UtcDateTime,
        SortOrder = e.SortOrder,
    };
}
