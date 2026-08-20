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

    [Required(ErrorMessage = "Enter the 6-digit code from your authenticator app.")]
    [StringLength(7, MinimumLength = 6, ErrorMessage = "The code is 6 digits.")]
    [Display(Name = "Verification code")]
    public string? VerificationCode { get; set; }
}

public record ConfirmEmailResultViewModel(bool Succeeded, string? Error);
