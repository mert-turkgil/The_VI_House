using System.ComponentModel.DataAnnotations;
using VIHouse.Business.Abstract;
using VIHouse.Entities.Referrals;
using VIHouse.WebUI.ViewModels.Ambassador;

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
    [Display(Name = "Admin.Field.Status")]
    public AmbassadorStatus Status { get; set; }

    public AmbassadorStats? Stats { get; set; }

    // --- Read-only, for the page ----------------------------------------------------------------

    public Guid? UserId { get; set; }

    /// <summary>The whole row, for the invitation, terms and payout panels.</summary>
    public Ambassador Ambassador { get; set; } = default!;

    /// <summary>Real name on the account, as given when they accepted.</summary>
    public string? AccountName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The absolute /r/{code} link — what an admin copies or emails. Built from
    /// Site:BaseUrl so it is the public host even when the panel is opened by IP or locally.</summary>
    public string ReferralUrl { get; set; } = "";

    /// <summary>The link as an inline SVG QR code, for a slide or a printed card.</summary>
    public string QrSvg { get; set; } = "";

    /// <summary>The site link plus one per experience and session — see Views/Shared/_ReferralLinks.</summary>
    public VIHouse.WebUI.ViewModels.Ambassador.ReferralLinksViewModel? Links { get; set; }

    public List<ReferralConversion> Conversions { get; set; } = [];
    public List<ReferralSourceCount> VisitSources { get; set; } = [];

    public List<ReferralPayout> Payouts { get; set; } = [];
    public ReferralFraudSignals? Signals { get; set; }

    /// <summary>Finance and SuperAdmin record payouts, void commission and keep the bank details;
    /// Marketing sees the numbers and the masked IBAN.</summary>
    public bool CanSettle { get; set; }

    /// <summary>Marketing and SuperAdmin keep the public profile: bio, niche, channels, photo.</summary>
    public bool CanEditProfile { get; set; }

    /// <summary>The checklist: what is still missing before they can be paid (see Ambassador.MissingRequirements).</summary>
    public IReadOnlyList<InfluencerRequirement> Missing { get; set; } = [];

    public InfluencerProfileForm? ProfileForm { get; set; }
    public InfluencerPayoutForm? PayoutForm { get; set; }

    /// <summary>True when a refused save of the bank details comes back: the form opens unfolded.</summary>
    public bool EditPayoutDetails { get; set; }

    public string? PhotoUrl { get; set; }

    /// <summary>Their journal articles, every status — the editors' way in to a submission.</summary>
    public List<VIHouse.Entities.Journal.JournalPost> Posts { get; set; } = [];

    public List<ReferralWithdrawalRequest> Withdrawals { get; set; } = [];

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
