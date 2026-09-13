using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.ViewModels.Account;

/// <summary>The account's membership page: the current membership, what to do about it, and the record.</summary>
public class AccountMembershipViewModel
{
    public MembershipSummary? Current { get; set; }
    public List<MembershipSummary> History { get; set; } = [];
    public bool CanManageBilling { get; set; }
    public string? MemberNumber { get; set; }

    /// <summary>Whether plans are for sale at all (FeatureOptions.MembershipSales). Off means the
    /// route in is an application, and the page says so instead of showing an empty plan grid.</summary>
    public bool SalesOpen { get; set; }

    public List<Membership.MembershipPlanCardViewModel> Plans { get; set; } = [];
}
