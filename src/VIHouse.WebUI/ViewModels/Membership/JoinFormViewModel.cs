using System.ComponentModel.DataAnnotations;
using VIHouse.Entities.Membership;

namespace VIHouse.WebUI.ViewModels.Membership;

/// <summary>
/// The join-and-pay form for a visitor with no account. Deliberately short: everything else about
/// them (company, bio, what they're looking for) is asked later, on their profile — this form only
/// collects what's needed to take a payment and create an account.
/// </summary>
public class JoinFormViewModel
{
    [Required(ErrorMessage = "Choose a plan.")]
    [Display(Name = "Plan")]
    public Guid PlanId { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = default!;

    [Required, StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = default!;

    [Required, EmailAddress, StringLength(256)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = default!;

    [Required, StringLength(2, MinimumLength = 2, ErrorMessage = "Use the two-letter country code, e.g. GB.")]
    [Display(Name = "Country")]
    public string Country { get; set; } = default!;

    [StringLength(120)]
    [Display(Name = "City")]
    public string? City { get; set; }

    [Display(Name = "I agree to the Terms & Conditions and Privacy Policy")]
    public bool AgreeToTerms { get; set; }

    public string? ReferralCode { get; set; }

    /// <summary>Repopulated on every render — the plan cards are part of the form, not a separate page.</summary>
    public List<MembershipPlan> Plans { get; set; } = [];

    /// <summary>The single-event alternative, offered on the same page so a visitor who only wants
    /// one gathering doesn't have to guess that /apply exists.</summary>
    public List<Experiences.ExperienceCardViewModel> OpenExperiences { get; set; } = [];
}

/// <summary>
/// The post-checkout landing page. <paramref name="SetupUrl"/> is null when the payment hasn't been
/// confirmed yet (the webhook can lag the browser redirect by a second or two) or when the account
/// already has a password — an existing member topping up, for instance.
/// </summary>
public record JoinSuccessViewModel(VIHouse.Business.Abstract.MembershipConfirmationInfo Info, string? SetupUrl);
