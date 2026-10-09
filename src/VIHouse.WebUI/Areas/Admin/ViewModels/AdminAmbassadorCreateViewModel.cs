using System.ComponentModel.DataAnnotations;
using VIHouse.WebUI.ViewModels.Ambassador;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// The invitation: the House enters everything about the influencer — who they are publicly, who
/// they are legally, where the money goes — and the invitee only chooses a password and accepts the
/// terms (AmbassadorInviteController).
/// </summary>
public class AdminAmbassadorCreateViewModel
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; set; } = default!;

    [Required, StringLength(150)]
    public string Name { get; set; } = default!;

    [Required, StringLength(40)]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Admin.Validation.LettersNumbersAndHyphensOnlyThis")]
    public string Code { get; set; } = default!;

    [Required, Range(0, 100)]
    public decimal CommissionPercent { get; set; }

    /// <summary>Language of the invitation email and page.</summary>
    public string Culture { get; set; } = VIHouse.Business.Options.SiteCultures.Default;

    public InfluencerProfileForm Profile { get; set; } = new InfluencerProfileForm().Padded();

    public InfluencerPayoutForm Payout { get; set; } = new();

    public IFormFile? Photo { get; set; }
}
