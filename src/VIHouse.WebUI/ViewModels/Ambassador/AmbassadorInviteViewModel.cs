using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.ViewModels.Ambassador;

/// <summary>
/// The invitation page: password, real name, billing address, bank details and the terms, in one
/// go. The error messages are resource keys (DataAnnotations localization reads them through
/// SharedResource), so the page speaks the invitation's language.
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

    /// <summary>False when the address already has a login and they are signed in with it.</summary>
    public bool NeedsPassword { get; set; } = true;

    /// <summary>The numbered terms, already formatted with the rate — exactly what gets stored.</summary>
    public List<string> Terms { get; set; } = [];

    // --- Posted ------------------------------------------------------------------------------------
    [Required(ErrorMessage = "AmbassadorInvite.Error.FirstName"), StringLength(100)]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "AmbassadorInvite.Error.LastName"), StringLength(100)]
    public string LastName { get; set; } = "";

    [DataType(DataType.Password), StringLength(100)]
    public string? Password { get; set; }

    [DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "AmbassadorInvite.Error.PasswordMismatch")]
    public string? ConfirmPassword { get; set; }

    [Required(ErrorMessage = "AmbassadorInvite.Error.Address"), StringLength(200)]
    public string AddressLine1 { get; set; } = "";

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [Required(ErrorMessage = "AmbassadorInvite.Error.City"), StringLength(100)]
    public string City { get; set; } = "";

    [Required(ErrorMessage = "AmbassadorInvite.Error.PostalCode"), StringLength(20)]
    public string PostalCode { get; set; } = "";

    [Required(ErrorMessage = "AmbassadorInvite.Error.Country")]
    public string Country { get; set; } = "";

    [StringLength(40)]
    public string? TaxId { get; set; }

    [Required(ErrorMessage = "AmbassadorInvite.Error.AccountHolder"), StringLength(150)]
    public string AccountHolder { get; set; } = "";

    [Required(ErrorMessage = "AmbassadorInvite.Error.Iban"), StringLength(50)]
    public string Iban { get; set; } = "";

    [StringLength(15)]
    public string? Bic { get; set; }

    public bool AcceptTerms { get; set; }
}
