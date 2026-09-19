using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.ViewModels.Checkout;

public class CheckoutSuccessViewModel(BookingConfirmationInfo info)
{
    public BookingConfirmationInfo Info { get; } = info;
}
