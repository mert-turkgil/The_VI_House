using VIHouse.Entities.Marketing;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>One page of the launch list, plus what the pager and the send form need.</summary>
public class AdminNotifySignupsViewModel
{
    public List<NotifySignup> Rows { get; set; } = [];

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }

    /// <summary>Addresses that have not been sent an announcement yet — the default audience.</summary>
    public int PendingCount { get; set; }

    /// <summary>What the admin typed, kept when a send is refused so nothing has to be retyped.</summary>
    public LaunchAnnouncementForm Draft { get; set; } = new();

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public class LaunchAnnouncementForm
{
    public string? Subject { get; set; }
    public string? Headline { get; set; }
    public string? Message { get; set; }
    public string? ButtonLabel { get; set; }
    public string? ButtonUrl { get; set; }

    /// <summary>Send to people who already got an earlier announcement as well.</summary>
    public bool IncludeAlreadyNotified { get; set; }

    /// <summary>The "I have checked this" tick — required for the real send, not for a test.</summary>
    public bool Confirmed { get; set; }
}
