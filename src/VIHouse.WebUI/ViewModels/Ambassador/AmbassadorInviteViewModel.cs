using System.ComponentModel.DataAnnotations;
using VIHouse.Entities.Referrals;

namespace VIHouse.WebUI.ViewModels.Ambassador;

/// <summary>
/// The invitation page. The House has already entered everything about the influencer; the page
/// shows it back and asks only for a password and acceptance of the terms. The error messages are
/// resource keys (DataAnnotations localization reads them through SharedResource), so the page
/// speaks the invitation's language.
/// </summary>
public class AmbassadorInviteViewModel
{
    // --- Shown, not posted -------------------------------------------------------------------------
    public string Token { get; set; } = "";
    public string AmbassadorName { get; set; } = "";
    public string Code { get; set; } = "";
    public decimal CommissionPercent { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string Email { get; set; } = "";

    /// <summary>What the House entered, shown so the invitee can say if something is wrong.</summary>
    public string? LegalName { get; set; }
    public string? BillingPlace { get; set; }
    public string? MaskedIban { get; set; }
    public List<AmbassadorChannel> Channels { get; set; } = [];

    /// <summary>False when the address already has a login and they are signed in with it.</summary>
    public bool NeedsPassword { get; set; } = true;

    /// <summary>The numbered terms, already formatted with the rate — exactly what gets stored.</summary>
    public List<string> Terms { get; set; } = [];

    // --- Posted ------------------------------------------------------------------------------------
    [DataType(DataType.Password), StringLength(100)]
    public string? Password { get; set; }

    [DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "AmbassadorInvite.Error.PasswordMismatch")]
    public string? ConfirmPassword { get; set; }

    public bool AcceptTerms { get; set; }
}
