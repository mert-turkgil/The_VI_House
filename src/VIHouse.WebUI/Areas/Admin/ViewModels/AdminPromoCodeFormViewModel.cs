using System.ComponentModel.DataAnnotations;
using VIHouse.Entities.Commerce;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// One discount code (brief §50). The engine has been in the payment path since Phase 1 — this is
/// the screen it never had, which is why codes could only be created with SQL.
/// </summary>
public class AdminPromoCodeFormViewModel
{
    public Guid? Id { get; set; }

    /// <summary>
    /// Stored and matched uppercase. A code is typed by hand at checkout, where "vi250" and "VI250"
    /// are obviously the same thing to the person typing and not to a database comparison.
    /// </summary>
    [Required, StringLength(40)]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Use letters, numbers and hyphens only.")]
    [Display(Name = "Code", Description = "What the member types at checkout, e.g. VI250.")]
    public string Code { get; set; } = default!;

    [Display(Name = "Type")]
    public PromoCodeType Type { get; set; } = PromoCodeType.Fixed;

    /// <summary>
    /// Entered in whole currency units for a fixed discount and converted to the minor units the
    /// entity stores, exactly as the ticket price is on the experience form — an admin thinks in
    /// euros, the money layer thinks in cents. A percentage is the number itself.
    /// </summary>
    [Range(0, 100000)]
    [Display(Name = "Value", Description = "A percentage (0–100), or an amount in whole currency units.")]
    public decimal Value { get; set; }

    [StringLength(3)]
    [Display(Name = "Currency", Description = "For a fixed amount. Leave blank for a percentage.")]
    public string? Currency { get; set; } = "GBP";

    [Display(Name = "Experience", Description = "Restrict the code to one experience, or leave blank for any.")]
    public Guid? ExperienceId { get; set; }

    [Display(Name = "Maximum redemptions", Description = "Blank for unlimited.")]
    [Range(1, 100000)]
    public int? MaxRedemptions { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Expires (UTC)", Description = "Blank never expires.")]
    public DateTime? ExpiresAt { get; set; }

    [Display(Name = "Active", Description = "Untick to stop a code being accepted without deleting its history.")]
    public bool IsActive { get; set; } = true;

    public PromoCode ToEntity() => new()
    {
        Id = Id ?? Guid.NewGuid(),
        Code = Code.Trim().ToUpperInvariant(),
        Type = Type,
        Value = Type == PromoCodeType.Percentage
            ? (int)Math.Round(Value, MidpointRounding.AwayFromZero)
            : (int)Math.Round(Value * 100m, MidpointRounding.AwayFromZero),
        // A percentage has no currency. Storing one would invite the reading that a "10%" code is
        // somehow GBP-only.
        Currency = Type == PromoCodeType.Percentage || string.IsNullOrWhiteSpace(Currency)
            ? null
            : Currency.Trim().ToUpperInvariant(),
        ExperienceId = ExperienceId,
        MaxRedemptions = MaxRedemptions,
        ExpiresAt = ExpiresAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(ExpiresAt.Value, DateTimeKind.Utc)),
        IsActive = IsActive,
    };

    public static AdminPromoCodeFormViewModel FromEntity(PromoCode c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Type = c.Type,
        Value = c.Type == PromoCodeType.Percentage ? c.Value : c.Value / 100m,
        Currency = c.Currency,
        ExperienceId = c.ExperienceId,
        MaxRedemptions = c.MaxRedemptions,
        ExpiresAt = c.ExpiresAt?.UtcDateTime,
        IsActive = c.IsActive,
    };

    /// <summary>Validates the pair the attributes above cannot see together: a percentage has to be
    /// a percentage.</summary>
    public string? Validate() =>
        Type == PromoCodeType.Percentage && Value is < 1 or > 100
            ? "A percentage discount must be between 1 and 100."
            : null;
}

/// <summary>One row of the promo code index.</summary>
public class AdminPromoCodeListItemViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public PromoCodeType Type { get; set; }
    public int Value { get; set; }
    public string? Currency { get; set; }
    public string? ExperienceTitle { get; set; }
    public int RedemptionCount { get; set; }
    public int? MaxRedemptions { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsActive { get; set; }

    /// <summary>What the code is worth, written the way it will be applied.</summary>
    public string ValueLabel => Type == PromoCodeType.Percentage
        ? $"{Value}%"
        : MoneyFormatter.Format(Value, Currency ?? "GBP");

    /// <summary>
    /// False when the code would be refused today — expired, switched off, or fully redeemed. The
    /// index shows this rather than IsActive alone, because "Active" against a code that has run out
    /// of redemptions is the kind of half-truth that has someone re-issuing it at a dinner.
    /// </summary>
    public bool IsUsable =>
        IsActive
        && (ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow)
        && (MaxRedemptions is null || RedemptionCount < MaxRedemptions);
}
