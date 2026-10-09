using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.ViewModels.Account;

public class BenefitsPageViewModel
{
    /// <summary>Null when the viewer holds no current membership.</summary>
    public MemberBenefits? Benefits { get; set; }
    public MembershipSummary? Membership { get; set; }
    public FounderPerks Founder { get; set; } = FounderPerks.None;
    public string Culture { get; set; } = "";

    /// <summary>Whether plans are on sale (Features:MembershipSales) — decides where a non-member is sent.</summary>
    public bool MembershipSales { get; set; }
}
