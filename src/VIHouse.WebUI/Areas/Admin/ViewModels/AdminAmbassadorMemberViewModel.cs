using System.ComponentModel.DataAnnotations;
using VIHouse.DataAccess.Abstract;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// "An existing member" on the Invite an influencer page: find the person, pick them, set the code
/// and the rate. They already have a login, so they are an influencer at once; the rest of their
/// profile is filled in afterwards, by the House or by them.
/// </summary>
public class AdminAmbassadorMemberViewModel
{
    /// <summary>The search box — name or email.</summary>
    public string? Query { get; set; }

    public IReadOnlyList<UserDirectoryRow> Results { get; set; } = [];

    /// <summary>The person picked from the results; null while still searching.</summary>
    public Guid? UserId { get; set; }

    public string? MemberName { get; set; }
    public string? MemberEmail { get; set; }

    /// <summary>Set when the picked person already has a referral record — they cannot get a second.</summary>
    public Guid? ExistingAmbassadorId { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    [Required, StringLength(40)]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Admin.Validation.LettersDigitsAndHyphensOnly")]
    public string Code { get; set; } = "";

    [Range(0, 100)]
    public decimal CommissionPercent { get; set; } = 10;
}
