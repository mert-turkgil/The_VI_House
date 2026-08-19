using VIHouse.Entities.Journal;

namespace VIHouse.WebUI.Helpers;

public static class JournalCategoryFormatter
{
    public static string ToDisplayLabel(this JournalCategory category) => category switch
    {
        JournalCategory.FounderStories => "Founder Stories",
        JournalCategory.HouseNotes => "House Notes",
        _ => category.ToString(),
    };
}
