using VIHouse.Entities.Applications;

namespace VIHouse.WebUI.Helpers;

public static class ApplicationStatusFormatter
{
    public static string ToDisplayLabel(this ApplicationStatus status) => status switch
    {
        ApplicationStatus.UnderReview => "Under Review",
        ApplicationStatus.PaymentPending => "Payment Pending",
        _ => status.ToString(),
    };
}
