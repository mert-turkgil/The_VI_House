using System.ComponentModel.DataAnnotations;
using VIHouse.Entities.Experiences;
using VIHouse.WebUI.Validation;

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
    public string Title { get; set; } = default!;

    [Required, StringLength(200)]
    [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Lowercase letters, numbers and hyphens only.")]
    public string Slug { get; set; } = default!;

    [StringLength(400)]
    public string? ShortSummary { get; set; }

    [Required]
    public string Description { get; set; } = default!;

    [Required, StringLength(100)]
    public string City { get; set; } = default!;

    [Required, StringLength(2)]
    public string Country { get; set; } = default!;

    [StringLength(200)]
    public string? Venue { get; set; }

    [Required, StringLength(100)]
    public string TimeZoneId { get; set; } = "Europe/London";

    [Required]
    [Display(Name = "Start (UTC)")]
    [DataType(DataType.DateTime)]
    public DateTime StartAtUtc { get; set; }

    [Required]
    [Display(Name = "End (UTC)")]
    [DataType(DataType.DateTime)]
    public DateTime EndAtUtc { get; set; }

    [Range(1, 1000)]
    public int Capacity { get; set; } = 20;

    public ExperienceStatus Status { get; set; } = ExperienceStatus.Draft;
    public ExperienceVisibility Visibility { get; set; } = ExperienceVisibility.Public;

    [Display(Name = "How can it be attended?")]
    public ExperienceAttendanceMode AttendanceMode { get; set; } = ExperienceAttendanceMode.InPerson;

    [StringLength(1000)]
    [SiteImageUrl]
    [Display(Name = "Cover image URL")]
    public string? CoverImageUrl { get; set; }

    /// <summary>
    /// Usually left empty, and that is right: the cover sits directly above the heading that names
    /// the experience, so describing it again is noise to a screen reader. Fill it only when the
    /// photograph carries something the surrounding copy does not.
    /// </summary>
    [StringLength(300)]
    [Display(Name = "Cover image description")]
    public string? CoverImageAlt { get; set; }

    /// <summary>Comma-separated, rendered as the chips under "Who is in the room?".</summary>
    [StringLength(300)]
    [Display(Name = "Who is in the room? (comma separated)")]
    public string? AudienceTags { get; set; }

    [Display(Name = "Application Opens")]
    public DateTime? ApplicationOpenAt { get; set; }

    [Display(Name = "Application Closes")]
    public DateTime? ApplicationCloseAt { get; set; }

    [Display(Name = "Sales Open")]
    public DateTime? SalesOpenAt { get; set; }

    [Display(Name = "Sales Close")]
    public DateTime? SalesCloseAt { get; set; }

    [StringLength(200)]
    [Display(Name = "SEO Title")]
    public string? SeoTitle { get; set; }

    [StringLength(400)]
    [Display(Name = "SEO Description")]
    public string? SeoDescription { get; set; }

    /// <summary>The image a link to this experience shows when pasted into a chat or a social post.
    /// Falls back to the cover when empty.</summary>
    [StringLength(1000)]
    [SiteImageUrl]
    [Display(Name = "Social preview image URL")]
    public string? SeoOgImageUrl { get; set; }

    [Display(Name = "Show in homepage Signature grid")]
    public bool IsSignature { get; set; }

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
        CoverImageUrl = CoverImageUrl,
        CoverImageAlt = CoverImageAlt,
        AudienceTags = AudienceTags,
        ApplicationOpenAt = ToUtcOffset(ApplicationOpenAt),
        ApplicationCloseAt = ToUtcOffset(ApplicationCloseAt),
        SalesOpenAt = ToUtcOffset(SalesOpenAt),
        SalesCloseAt = ToUtcOffset(SalesCloseAt),
        SeoTitle = SeoTitle,
        SeoDescription = SeoDescription,
        SeoOgImageUrl = SeoOgImageUrl,
        IsSignature = IsSignature,
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
        SortOrder = e.SortOrder,
    };

    private static DateTimeOffset? ToUtcOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}
