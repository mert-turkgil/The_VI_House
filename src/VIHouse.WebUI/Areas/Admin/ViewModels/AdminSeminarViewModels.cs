using System.ComponentModel.DataAnnotations;
using VIHouse.Business.Options;
using VIHouse.Entities.Seminars;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// The core (non-localised, non-collection) fields of a seminar: when it runs, what it costs, who
/// may see it. Every word a reader will actually read lives on
/// <see cref="AdminSeminarTranslationFormViewModel"/> instead, one form per language.
///
/// Start/End bind as plain DateTime from &lt;input type="datetime-local"&gt; and are treated as UTC
/// directly — the same Phase 1 simplification AdminExperienceFormViewModel makes, and the reason
/// both labels say "(UTC)" out loud.
/// </summary>
public class AdminSeminarFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(200)]
    [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Seminar.Validation.Slug")]
    [Display(Name = "Admin.Seminar.Slug")]
    public string Slug { get; set; } = default!;

    [Display(Name = "Admin.Seminar.Visibility")]
    public SeminarVisibility Visibility { get; set; } = SeminarVisibility.Members;

    [StringLength(150)]
    [Display(Name = "Admin.Seminar.HostName")]
    public string? HostName { get; set; }

    [StringLength(150)]
    [Display(Name = "Admin.Seminar.HostTitle")]
    public string? HostTitle { get; set; }

    [Display(Name = "Admin.Seminar.IsOnline")]
    public bool IsOnline { get; set; } = true;

    [StringLength(200)]
    [Display(Name = "Admin.Seminar.Location")]
    public string? Location { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Admin.Seminar.TimeZone")]
    public string TimeZoneId { get; set; } = "Europe/London";

    /// <summary>Null for on-demand content — a recording has nothing to turn up to.</summary>
    [Display(Name = "Admin.Seminar.StartAt")]
    [DataType(DataType.DateTime)]
    public DateTime? StartAtUtc { get; set; }

    [Display(Name = "Admin.Seminar.EndAt")]
    [DataType(DataType.DateTime)]
    public DateTime? EndAtUtc { get; set; }

    [Range(0, 10000)]
    [Display(Name = "Admin.Seminar.Capacity")]
    public int Capacity { get; set; }

    /// <summary>
    /// Entered in whole currency units because that is what an admin thinks in; converted to the
    /// integer minor units everything else stores (brief §184) in <see cref="ToEntity"/>. Decimal,
    /// not double — this is money, and the conversion happens exactly once, here.
    /// </summary>
    [Range(0, 100000)]
    [Display(Name = "Admin.Seminar.Price")]
    public decimal Price { get; set; }

    [Required, StringLength(3)]
    [Display(Name = "Admin.Seminar.Currency")]
    public string Currency { get; set; } = "GBP";

    [Display(Name = "Admin.Seminar.IncludedWithMembership")]
    public bool IncludedWithMembership { get; set; } = true;

    [Display(Name = "Admin.Seminar.SortOrder")]
    public int SortOrder { get; set; }

    public Seminar ToEntity() => new()
    {
        Id = Id ?? Guid.NewGuid(),
        Slug = Slug,
        Visibility = Visibility,
        HostName = HostName,
        HostTitle = HostTitle,
        IsOnline = IsOnline,
        Location = Location,
        TimeZoneId = TimeZoneId,
        StartAtUtc = ToUtcOffset(StartAtUtc),
        EndAtUtc = ToUtcOffset(EndAtUtc),
        Capacity = Capacity,
        PriceMinor = (long)Math.Round(Price * 100m, MidpointRounding.AwayFromZero),
        Currency = Currency.ToUpperInvariant(),
        IncludedWithMembership = IncludedWithMembership,
        SortOrder = SortOrder,
    };

    public static AdminSeminarFormViewModel FromEntity(Seminar s) => new()
    {
        Id = s.Id,
        Slug = s.Slug,
        Visibility = s.Visibility,
        HostName = s.HostName,
        HostTitle = s.HostTitle,
        IsOnline = s.IsOnline,
        Location = s.Location,
        TimeZoneId = s.TimeZoneId,
        StartAtUtc = s.StartAtUtc?.UtcDateTime,
        EndAtUtc = s.EndAtUtc?.UtcDateTime,
        Capacity = s.Capacity,
        Price = s.PriceMinor / 100m,
        Currency = s.Currency,
        IncludedWithMembership = s.IncludedWithMembership,
        SortOrder = s.SortOrder,
    };

    private static DateTimeOffset? ToUtcOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}

/// <summary>One language's copy. The same form serves every culture; which one is being edited is
/// carried in <see cref="Culture"/>, posted as a hidden field.</summary>
public class AdminSeminarTranslationFormViewModel
{
    public Guid SeminarId { get; set; }

    [Required, StringLength(10)]
    public string Culture { get; set; } = SiteCultures.Default;

    [Required, StringLength(200)]
    [Display(Name = "Admin.Seminar.Title")]
    public string Title { get; set; } = default!;

    [StringLength(600)]
    [Display(Name = "Admin.Seminar.Summary")]
    public string? Summary { get; set; }

    [Required]
    [Display(Name = "Admin.Seminar.Body")]
    public string BodyHtml { get; set; } = default!;

    [StringLength(200)]
    [Display(Name = "Admin.Seminar.SeoTitle")]
    public string? SeoTitle { get; set; }

    [StringLength(400)]
    [Display(Name = "Admin.Seminar.SeoDescription")]
    public string? SeoDescription { get; set; }

    public SeminarTranslation ToEntity() => new()
    {
        SeminarId = SeminarId,
        Culture = Culture,
        Title = Title,
        Summary = Summary,
        BodyHtml = BodyHtml,
        SeoTitle = SeoTitle,
        SeoDescription = SeoDescription,
    };

    public static AdminSeminarTranslationFormViewModel FromEntity(Guid seminarId, SeminarTranslation t) => new()
    {
        SeminarId = seminarId,
        Culture = t.Culture,
        Title = t.Title,
        Summary = t.Summary,
        BodyHtml = t.BodyHtml,
        SeoTitle = t.SeoTitle,
        SeoDescription = t.SeoDescription,
    };

    /// <summary>An untouched form for a language that has no row yet.</summary>
    public static AdminSeminarTranslationFormViewModel Empty(Guid seminarId, string culture) => new()
    {
        SeminarId = seminarId,
        Culture = culture,
        Title = string.Empty,
        BodyHtml = string.Empty,
    };
}

/// <summary>
/// The Create screen. A seminar and its default-language copy are created together because a
/// seminar with no title in any language is not something the rest of the system can render — the
/// translation tabs for the other three appear once it exists.
/// </summary>
public class AdminSeminarCreateViewModel
{
    public AdminSeminarFormViewModel Seminar { get; set; } = new();
    public AdminSeminarTranslationFormViewModel DefaultTranslation { get; set; } = new();
}

/// <summary>
/// Everything the Edit screen shows at once: the core fields, one tab per language, the media
/// library, and how many people have signed up. Each part posts to its own action rather than one
/// giant bound form — saving the schedule must never silently discard a half-written German body.
/// </summary>
public class AdminSeminarEditViewModel
{
    public AdminSeminarFormViewModel Form { get; set; } = default!;

    public SeminarStatus Status { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? CoverMediaId { get; set; }

    /// <summary>One entry per supported language, in SiteCultures order, whether or not it has been
    /// written yet — the point of the screen is to show what is still missing.</summary>
    public List<AdminSeminarTranslationTab> Translations { get; set; } = [];

    public List<SeminarMedia> Media { get; set; } = [];
    public int EnrolmentCount { get; set; }

    /// <summary>The culture tab to open on load, carried through a redirect so saving German copy
    /// returns to the German tab rather than dumping the admin back on English.</summary>
    public string ActiveCulture { get; set; } = SiteCultures.Default;
}

/// <param name="IsWritten">False when no row exists for this culture yet.</param>
public record AdminSeminarTranslationTab(
    SiteCulture Culture, bool IsWritten, bool IsDefault, AdminSeminarTranslationFormViewModel Form);

/// <summary>One row of the admin index.</summary>
public class AdminSeminarListItemViewModel
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = default!;
    public string Title { get; set; } = default!;
    public SeminarStatus Status { get; set; }
    public SeminarVisibility Visibility { get; set; }
    public DateTimeOffset? StartAtUtc { get; set; }
    public long PriceMinor { get; set; }
    public string Currency { get; set; } = "GBP";
    public bool IncludedWithMembership { get; set; }

    /// <summary>Which languages have copy — the fastest way to spot a session that went live with
    /// three empty translations.</summary>
    public List<string> TranslatedCultures { get; set; } = [];
}

/// <summary>The attendee list for one seminar.</summary>
public class AdminSeminarEnrolmentsViewModel
{
    public Guid SeminarId { get; set; }
    public string SeminarTitle { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public List<AdminSeminarEnrolmentRow> Rows { get; set; } = [];
}

public record AdminSeminarEnrolmentRow(
    string Email,
    string Name,
    SeminarEnrollmentStatus Status,
    SeminarAccessGrant GrantedVia,
    long AmountMinor,
    string Currency,
    DateTimeOffset? ConfirmedAt);
