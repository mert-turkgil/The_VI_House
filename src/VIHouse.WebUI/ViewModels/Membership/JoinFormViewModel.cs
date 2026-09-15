using System.ComponentModel.DataAnnotations;
using VIHouse.Entities.Membership;

namespace VIHouse.WebUI.ViewModels.Membership;

/// <summary>
/// The join-and-pay form for a visitor with no account. Asks the House's one set of questions —
/// the same ones /apply asks and the account profile holds — so a member who joins directly
/// arrives with a complete profile rather than an empty one to fill in later.
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

    [StringLength(200)]
    [Display(Name = "Title / profession")]
    public string? JobTitle { get; set; }

    [StringLength(200)]
    [Display(Name = "Address line 1")]
    public string? AddressLine1 { get; set; }

    [StringLength(200)]
    [Display(Name = "Address line 2")]
    public string? AddressLine2 { get; set; }

    [StringLength(20)]
    [Display(Name = "Postal code")]
    public string? PostalCode { get; set; }

    [StringLength(120)]
    [Display(Name = "City")]
    public string? City { get; set; }

    // See ApplyFormViewModel.Country for why there is no [StringLength] here.
    [Required(ErrorMessage = "Choose your country.")]
    [Display(Name = "Country")]
    public string Country { get; set; } = default!;

    [Required(ErrorMessage = "Tell us a little about yourself or your business.")]
    [StringLength(2000)]
    [Display(Name = "Describe yourself or your business")]
    public string About { get; set; } = default!;

    [Required(ErrorMessage = "Tell us what you're hoping for.")]
    [StringLength(2000)]
    [Display(Name = "How may we help you with your goals, or what are your expectations?")]
    public string Expectations { get; set; } = default!;

    [StringLength(20)]
    [Display(Name = "Annual earnings")]
    public string? EarningsBand { get; set; }

    [Display(Name = "I agree to the Terms & Conditions and Privacy Policy")]
    public bool AgreeToTerms { get; set; }

    public string? ReferralCode { get; set; }

    [StringLength(40)]
    public string? PromoCode { get; set; }

    /// <summary>Repopulated on every render — the plan cards are part of the form, not a separate page.</summary>
    public List<MembershipPlan> Plans { get; set; } = [];

    /// <summary>Plans at their member limit — rendered disabled with a note, never selectable.</summary>
    public HashSet<Guid> FullPlanIds { get; set; } = [];

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

/// <summary>
/// The resume page: what an abandoned checkout was for, and whether a new one can be opened.
/// <paramref name="SalesOpen"/> is false when Features:MembershipSales is off — the page still
/// renders (it is linked from an email) but offers no button.
/// </summary>
public record JoinResumeViewModel(VIHouse.Business.Abstract.PendingJoinInfo Info, bool SalesOpen);
