using VIHouse.Entities.Marketing;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>One page of the launch list, plus what the pager needs.</summary>
public class AdminNotifySignupsViewModel
{
    public List<NotifySignup> Rows { get; set; } = [];

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
