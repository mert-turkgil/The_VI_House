using VIHouse.Entities.Experiences;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// Wraps the core-fields form plus every child collection shown on the same Edit page. Each has its
/// own small add/edit/remove POST actions rather than one giant bound form — avoids the complexity
/// of dynamic-index array model binding for what is fundamentally a set of short admin lists.
///
/// The gallery and the programme were readable on the public page and editable nowhere until now;
/// they are loaded by the repository either way, and were simply being dropped on the floor here.
/// </summary>
public class AdminExperienceEditViewModel
{
    public AdminExperienceFormViewModel Form { get; set; } = default!;
    public List<TicketType> TicketTypes { get; set; } = [];
    public List<ExperienceInclusion> Inclusions { get; set; } = [];
    public List<ExperienceFaq> Faqs { get; set; } = [];
    public List<ExperienceImage> Gallery { get; set; } = [];
    public List<ExperienceProgramDay> ProgramDays { get; set; } = [];

    /// <summary>Where to show the cover from — the uploaded file, or the typed path.</summary>
    public string? CoverPreviewUrl { get; set; }

    /// <summary>Every plan that exists, with a flag for whether this experience admits it.</summary>
    public List<ExperienceMemberAccessOption> MemberAccess { get; set; } = [];

    /// <summary>True once a cover has been uploaded. The URL box is disabled while it is, because an
    /// upload wins outright and a live-looking text field that changes nothing is worse than a
    /// greyed-out one.</summary>
    public bool HasUploadedCover { get; set; }
}

/// <param name="Admitted">Whether holders of this plan can join without applying.</param>
public record ExperienceMemberAccessOption(Guid PlanId, string Name, string PriceLabel, bool Admitted);
