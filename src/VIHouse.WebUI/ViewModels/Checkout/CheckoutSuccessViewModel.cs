using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.ViewModels.Checkout;

public class CheckoutSuccessViewModel(BookingConfirmationInfo info, string? passwordSetupUrl)
{
    public BookingConfirmationInfo Info { get; } = info;
    public string? PasswordSetupUrl { get; } = passwordSetupUrl;
}
