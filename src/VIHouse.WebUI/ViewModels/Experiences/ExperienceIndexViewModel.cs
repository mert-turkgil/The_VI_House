using VIHouse.Entities.Experiences;

namespace VIHouse.WebUI.ViewModels.Experiences;

/// <summary>
/// The listing page, which used to be a bare <c>List&lt;ExperienceCardViewModel&gt;</c>.
///
/// It needs more than the cards because the empty state has to say two different things. "No
/// experiences match those filters" (with a way to clear them) and "nothing is open at the moment"
/// are completely different messages, and a list on its own cannot tell them apart — which is why
/// the old page showed the same sentence for both and left anyone who mistyped a city believing the
/// site had no events at all.
/// </summary>
public class ExperienceIndexViewModel
{
    public List<ExperienceCardViewModel> Cards { get; init; } = [];

    /// <summary>Cities that actually have something listed, for the filter dropdown.</summary>
    public List<string> Cities { get; init; } = [];

    public string? SelectedCity { get; init; }
    public ExperienceStatus? SelectedStatus { get; init; }

    public bool HasFilters => !string.IsNullOrWhiteSpace(SelectedCity) || SelectedStatus is not null;

    /// <summary>The statuses worth offering as chips. Draft and Completed are deliberately absent:
    /// draft is never publicly listed, and "completed" is a filter for a past nobody is shopping
    /// for.</summary>
    public static readonly ExperienceStatus[] FilterableStatuses =
    [
        ExperienceStatus.ApplicationsOpen,
        ExperienceStatus.AlmostFull,
        ExperienceStatus.Waitlist,
        ExperienceStatus.ComingSoon,
    ];
}
