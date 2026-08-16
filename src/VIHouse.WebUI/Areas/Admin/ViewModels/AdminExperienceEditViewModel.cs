using VIHouse.Entities.Experiences;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// Wraps the core-fields form plus the child collections (ticket types, inclusions, FAQs) shown
/// on the same Edit page. Each child collection has its own small add/remove POST actions
/// (AddTicketType, RemoveTicketType, ...) rather than one giant bound form — avoids the
/// complexity of dynamic-index array model binding for what's fundamentally a short admin list.
/// </summary>
public class AdminExperienceEditViewModel
{
    public AdminExperienceFormViewModel Form { get; set; } = default!;
    public List<TicketType> TicketTypes { get; set; } = [];
    public List<ExperienceInclusion> Inclusions { get; set; } = [];
    public List<ExperienceFaq> Faqs { get; set; } = [];
}
