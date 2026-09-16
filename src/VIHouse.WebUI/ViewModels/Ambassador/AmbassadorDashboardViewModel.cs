using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.ViewModels.Ambassador;

public class AmbassadorDashboardViewModel
{
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public decimal CommissionPercent { get; set; }
    public AmbassadorStats Stats { get; set; } = default!;

    /// <summary>The ledger, newest first — see IAmbassadorService.GetConversionsAsync.</summary>
    public List<VIHouse.Entities.Referrals.ReferralConversion> Conversions { get; set; } = [];

    /// <summary>The site link plus one link per experience and session, with QR codes.</summary>
    public ReferralLinksViewModel Links { get; set; } = default!;
}
