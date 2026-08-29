using VIHouse.Entities.Communication;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>One page of the message log, with what the pager and the filter chips need.</summary>
public class AdminEmailLogViewModel
{
    /// <summary>Which channel is on screen. One screen rather than two because the question an admin
    /// arrives with — "did it reach them" — doesn't know in advance which way it was sent.</summary>
    public bool ShowSms { get; set; }

    public List<EmailLog> Rows { get; set; } = [];
    public List<SmsLog> SmsRows { get; set; } = [];

    /// <summary>False when no gateway is set up, so an empty SMS tab reads as "switched off" rather
    /// than "nothing was ever sent".</summary>
    public bool SmsConfigured { get; set; }

    /// <summary>Null when showing everything.</summary>
    public EmailStatus? Status { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }

    /// <summary>
    /// Failures across the whole log for the channel on screen, not just this page — shown regardless
    /// of the current filter, because the number that matters is "is this broken right now", and
    /// someone reading page 4 of the Sent list should still see it.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>Failures on the other channel, so a problem is visible from either tab.</summary>
    public int OtherChannelFailedCount { get; set; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
