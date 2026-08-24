using VIHouse.Entities.Common;

namespace VIHouse.Entities.Seminars;

/// <summary>
/// One culture's worth of a seminar's reader-facing copy. The site ships EN/DE/TR/ET, and a row
/// exists per culture the admin has actually written — a missing row falls back to the default
/// culture at read time rather than rendering an empty page.
///
/// <see cref="BodyHtml"/> is authored in the admin panel's rich text editor and rendered with
/// Html.Raw, so it is always written through SeminarService, which sanitises it (see EditorHtml).
/// Never assign to it from raw request input.
/// </summary>
public class SeminarTranslation : BaseEntity
{
    public Guid SeminarId { get; set; }

    /// <summary>Culture name exactly as RequestLocalizationOptions supplies it, e.g. "en-GB".</summary>
    public string Culture { get; set; } = default!;

    public string Title { get; set; } = default!;

    /// <summary>Shown on the listing card and above the enrolment call to action — this is the part
    /// someone reads *before* they have access, so it has to stand on its own.</summary>
    public string? Summary { get; set; }

    /// <summary>Sanitised HTML. Behind enrolment on the public page.</summary>
    public string BodyHtml { get; set; } = default!;

    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
}
