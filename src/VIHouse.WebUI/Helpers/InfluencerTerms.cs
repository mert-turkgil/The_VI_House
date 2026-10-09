using System.Globalization;
using Microsoft.Extensions.Localization;

namespace VIHouse.WebUI.Helpers;

/// <summary>
/// The influencer terms as numbered lines in the reader's language, the rate filled in. The same
/// lines are shown on the invitation page and on the terms card in the influencer area, and their
/// exact text is what the ConsentRecord stores (see AmbassadorTerms.Version).
/// </summary>
public static class InfluencerTerms
{
    public static List<string> Lines(IStringLocalizer loc, decimal commissionPercent)
    {
        var rate = commissionPercent.ToString("0.##", CultureInfo.CurrentCulture);
        return
        [
            loc["AmbassadorInvite.Terms.Commission", rate].Value,
            loc["AmbassadorInvite.Terms.Payouts"].Value,
            loc["AmbassadorInvite.Terms.Refunds"].Value,
            loc["AmbassadorInvite.Terms.OwnPurchases"].Value,
            loc["AmbassadorInvite.Terms.Journal"].Value,
            loc["AmbassadorInvite.Terms.Changes"].Value,
            loc["AmbassadorInvite.Terms.Privacy"].Value,
        ];
    }
}
