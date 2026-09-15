using System.ComponentModel.DataAnnotations;
using VIHouse.Business.Abstract;
using VIHouse.Entities.Referrals;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminAmbassadorEditViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string? Email { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = default!;

    [Required, Range(0, 100)]
    public decimal CommissionPercent { get; set; }

    [Required]
    public AmbassadorStatus Status { get; set; }

    public AmbassadorStats? Stats { get; set; }

    // --- Read-only, for the page ----------------------------------------------------------------

    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The absolute /r/{code} link — what an admin copies or emails. Built from
    /// Site:BaseUrl so it is the public host even when the panel is opened by IP or locally.</summary>
    public string ReferralUrl { get; set; } = "";

    /// <summary>The link as an inline SVG QR code, for a slide or a printed card.</summary>
    public string QrSvg { get; set; } = "";

    public List<ReferralConversion> Conversions { get; set; } = [];
    public List<ReferralSourceCount> VisitSources { get; set; } = [];

    public static AdminAmbassadorEditViewModel FromEntity(Ambassador a, string? email) => new()
    {
        Id = a.Id,
        Code = a.Code,
        Email = email,
        Name = a.Name,
        CommissionPercent = a.CommissionPercent,
        Status = a.Status,
        UserId = a.UserId,
        CreatedAt = a.CreatedAt,
    };

    public Ambassador ToEntity() => new()
    {
        Id = Id,
        Code = Code,
        Name = Name.Trim(),
        CommissionPercent = CommissionPercent,
        Status = Status,
    };
}
