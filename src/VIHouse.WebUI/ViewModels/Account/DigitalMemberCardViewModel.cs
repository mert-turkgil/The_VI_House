namespace VIHouse.WebUI.ViewModels.Account;

public class DigitalMemberCardViewModel
{
    public string FullName { get; set; } = default!;
    public string PlanName { get; set; } = default!;
    public string MemberNumber { get; set; } = default!;
    public DateTimeOffset MemberSince { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>A Founder with the badge switched on (Admin › Founders).</summary>
    public bool ShowFounderBadge { get; set; }
}
