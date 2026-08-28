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

    [StringLength(1000)]
    [SiteImageUrl]
    [Display(Name = "Cover Image URL")]
    public string? CoverImageUrl { get; set; }

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
        CoverImageUrl = CoverImageUrl,
        ApplicationOpenAt = ToUtcOffset(ApplicationOpenAt),
        ApplicationCloseAt = ToUtcOffset(ApplicationCloseAt),
        SalesOpenAt = ToUtcOffset(SalesOpenAt),
        SalesCloseAt = ToUtcOffset(SalesCloseAt),
        SeoTitle = SeoTitle,
        SeoDescription = SeoDescription,
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
        CoverImageUrl = e.CoverImageUrl,
        ApplicationOpenAt = e.ApplicationOpenAt?.UtcDateTime,
        ApplicationCloseAt = e.ApplicationCloseAt?.UtcDateTime,
        SalesOpenAt = e.SalesOpenAt?.UtcDateTime,
        SalesCloseAt = e.SalesCloseAt?.UtcDateTime,
        SeoTitle = e.SeoTitle,
        SeoDescription = e.SeoDescription,
        IsSignature = e.IsSignature,
        SortOrder = e.SortOrder,
    };

    private static DateTimeOffset? ToUtcOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}
