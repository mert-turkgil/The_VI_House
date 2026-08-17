using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.ViewModels.Checkout;

public class InvitationViewModel(string code, InvitationLandingInfo info, string? errorMessage)
{
    public string Code { get; } = code;
    public InvitationLandingInfo Info { get; } = info;
    public string? ErrorMessage { get; } = errorMessage;
}
