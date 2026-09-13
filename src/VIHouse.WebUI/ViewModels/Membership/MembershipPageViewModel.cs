using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.ViewModels.Membership;

/// <summary>
/// /membership, which reads three ways. A visitor gets the case for membership and the plans (or
/// the application route while sales are closed). A signed-in guest — ticket holder, session
/// attendee, or an account with nothing on it yet — gets the same page, addressed to them. A member
/// gets their own membership in place of the sales pitch: plan, term, what it unlocks, and the
/// doors it opens, with the plan cards demoted to "other plans".
/// </summary>
public class MembershipPageViewModel
{
    public List<MembershipPlanCardViewModel> Plans { get; set; } = [];

    public bool IsAuthenticated { get; set; }
    public string? FirstName { get; set; }

    /// <summary>The viewer's current membership, when they hold one.</summary>
    public MembershipSummary? Current { get; set; }
    public string? MemberNumber { get; set; }
    public bool CanManageBilling { get; set; }

    /// <summary>What a signed-in non-member already holds, so the page can address them as a
    /// guest of the House rather than a stranger.</summary>
    public int BookingCount { get; set; }
    public int SessionCount { get; set; }

    public bool SalesOpen { get; set; }
    public bool CommunityEnabled { get; set; }
    public bool DirectoryEnabled { get; set; }

    public bool IsMember => Current is not null;
    public bool IsGuest => IsAuthenticated && !IsMember && (BookingCount > 0 || SessionCount > 0);
}
