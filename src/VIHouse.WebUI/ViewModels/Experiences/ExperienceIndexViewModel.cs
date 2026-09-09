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

    /// <summary>The trending chip currently applied, if any — the literal label text ("AI tools"),
    /// not a code. There is no Experience.Topic column; this is a keyword the listing searches for
    /// in each experience's own copy. See EfExperienceRepository.GetPublicListingAsync.</summary>
    public string? SelectedTopic { get; init; }

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(SelectedCity) || SelectedStatus is not null || !string.IsNullOrWhiteSpace(SelectedTopic);

    /// <summary>
    /// The same seven keys the homepage's hero filter bar trends on (Home.Filter.Topic.*) — reused
    /// rather than duplicated, so there is one list of topics for the whole site, and so the
    /// homepage's existing "/experiences?topic=..." links (previously decorative — the listing
    /// ignored the parameter entirely) now land on a page that actually filters by them.
    /// </summary>
    public static readonly string[] TrendingTopicKeys =
    [
        "Home.Filter.Topic.Ecommerce",
        "Home.Filter.Topic.BrandBuilding",
        "Home.Filter.Topic.AiTools",
        "Home.Filter.Topic.Copywriting",
        "Home.Filter.Topic.PaidAds",
        "Home.Filter.Topic.Trading",
        "Home.Filter.Topic.Funding",
    ];

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
