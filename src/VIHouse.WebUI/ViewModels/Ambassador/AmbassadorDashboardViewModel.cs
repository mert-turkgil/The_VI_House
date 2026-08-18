using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.ViewModels.Ambassador;

public class AmbassadorDashboardViewModel
{
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public decimal CommissionPercent { get; set; }
    public AmbassadorStats Stats { get; set; } = default!;
}
