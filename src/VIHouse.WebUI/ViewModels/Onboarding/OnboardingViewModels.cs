using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.ViewModels.Onboarding;

public class OnboardingStatusViewModel
{
    public string FirstName { get; set; } = "";
    public string Email { get; set; } = "";

    public bool HasPassword { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }

    public string? PlanName { get; set; }

    /// <summary>True only for a paid-up member. A single-event ticket holder gets an account and a
    /// ticket but no membership, which is what withholds the community invites from them.</summary>
    public bool HasMembership { get; set; }

    /// <summary>True for an account holding any admin-side role. Staff arrive here through an
    /// invite rather than a purchase, so the page must not tell them their payment went through.</summary>
    public bool IsStaff { get; set; }

    /// <summary>The password step is excluded on purpose: it's completed before the member can sign
    /// in at all, so by the time this page renders it is always done.</summary>
    public bool IsComplete => EmailConfirmed && TwoFactorEnabled;

    public int CompletedSteps => (HasPassword ? 1 : 0) + (EmailConfirmed ? 1 : 0) + (TwoFactorEnabled ? 1 : 0);
    public int TotalSteps => 3;
}

public class TwoFactorSetupViewModel
{
    /// <summary>The authenticator secret, spaced in fours for manual entry.</summary>
    public string SharedKey { get; set; } = "";

    /// <summary>otpauth:// URI encoded into the QR code.</summary>
    public string AuthenticatorUri { get; set; } = "";

    // ErrorMessage/Name are resource *keys*, not literals: Program.cs points the DataAnnotations
    // localizer at SharedResource, so these resolve per-culture the same way the view strings do.
    // A key with no translation falls back to the key itself, which is why every one used here has
    // an entry in all four SharedResource.*.resx files.
    [Required(ErrorMessage = "Onboarding.TwoFactor.Error.Required")]
    [StringLength(7, MinimumLength = 6, ErrorMessage = "Onboarding.TwoFactor.Error.Length")]
    [Display(Name = "Onboarding.TwoFactor.CodeLabel")]
    public string? VerificationCode { get; set; }
}

/// <summary>
/// The one-time display of the recovery codes. Acknowledged is a posted tick box rather than a
/// plain link, so finishing the flow requires an explicit "I have these saved" — see
/// OnboardingController.RecoveryCodes.
/// </summary>
public class RecoveryCodesViewModel
{
    public List<string> Codes { get; set; } = [];

    [Display(Name = "Onboarding.Codes.Ack")]
    public bool Acknowledged { get; set; }
}

public record ConfirmEmailResultViewModel(bool Succeeded, string? Error);
