using System.ComponentModel.DataAnnotations;

namespace VIHouse.Entities.Journal;

// Brief §125's six Journal content categories. The [Display] names are what Html.GetEnumSelectList
// puts in the admin dropdown — without them the two compound names render as "FounderStories" and
// "HouseNotes".
public enum JournalCategory
{
    [Display(Name = "Founder Stories")]
    FounderStories,
    Business,
    Technology,
    Capital,
    Culture,

    [Display(Name = "House Notes")]
    HouseNotes
}
