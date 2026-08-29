using VIHouse.Business.Abstract;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Communication;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminApplicationDetailViewModel
{
    public Application Application { get; set; } = default!;
    public string ExperienceLabel { get; set; } = default!;
    public string? ReviewedByEmail { get; set; }
    public Invitation? Invitation { get; set; }
    public Payment? Payment { get; set; }
    public PaymentProviderDetails? LivePaymentDetails { get; set; }

    /// <summary>Everything sent to this applicant about this application, both channels — so "did
    /// their payment link go out" is answered on the screen where the decision was made.</summary>
    public List<EmailLog> Emails { get; set; } = [];
    public List<SmsLog> TextMessages { get; set; } = [];

    /// <summary>False when no SMS gateway is set up, so the screen can say that rather than leaving
    /// an admin wondering why nothing was texted.</summary>
    public bool SmsConfigured { get; set; }

    public bool CanMarkUnderReview => Application.Status == ApplicationStatus.Submitted;
    public bool CanShortlist => Application.Status == ApplicationStatus.UnderReview;

    /// <summary>
    /// Waitlisted is included deliberately: the waitlist is a holding pattern, not a verdict, and the
    /// answer changes when a seat opens. See ApplicationService's transition table.
    /// </summary>
    public bool CanDecide => Application.Status is ApplicationStatus.Shortlisted or ApplicationStatus.Waitlisted;

    /// <summary>Already waiting — so the review panel offers a decision rather than the Waitlist
    /// button they just pressed.</summary>
    public bool IsWaitlisted => Application.Status == ApplicationStatus.Waitlisted;

    /// <summary>An approved applicant who hasn't paid can always be sent their link again.</summary>
    public bool CanResendInvitation =>
        Application.Status is ApplicationStatus.Approved or ApplicationStatus.PaymentPending
        && Invitation is not { IsUsed: true };
}
