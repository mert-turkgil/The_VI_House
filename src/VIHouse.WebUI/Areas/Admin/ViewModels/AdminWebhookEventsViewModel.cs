using VIHouse.Entities.Commerce;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>The Stripe event log: one page of rows plus the counts the filter chips show.</summary>
public class AdminWebhookEventsViewModel
{
    public WebhookEventStatus? Status { get; set; }
    public List<WebhookEvent> Rows { get; set; } = [];
    public Dictionary<WebhookEventStatus, int> Counts { get; set; } = [];
    public int Take { get; set; }

    public int Count(WebhookEventStatus status) => Counts.GetValueOrDefault(status);
    public int Total => Counts.Values.Sum();
}
