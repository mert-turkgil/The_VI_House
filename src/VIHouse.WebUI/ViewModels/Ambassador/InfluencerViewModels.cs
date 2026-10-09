using System.ComponentModel.DataAnnotations;
using VIHouse.Business.Abstract;
using VIHouse.Entities.Journal;
using VIHouse.Entities.Referrals;

namespace VIHouse.WebUI.ViewModels.Ambassador;

/// <summary>The influencer's overview: what is still missing, the terms if not yet accepted, the
/// numbers, the links, and the latest activity. Aggregates only — never who was referred.</summary>
public class InfluencerOverviewViewModel
{
    public required Entities.Referrals.Ambassador Influencer { get; init; }
    public required AmbassadorStats Stats { get; init; }
    public IReadOnlyList<InfluencerRequirement> Missing { get; init; } = [];

    /// <summary>The terms to accept, when the influencer never saw the invitation page (made from an
    /// existing account). Null once accepted.</summary>
    public List<string>? Terms { get; init; }

    public List<ReferralConversion> Recent { get; init; } = [];
    public required ReferralLinksViewModel Links { get; init; }
}

/// <summary>Balances, withdrawal requests, payouts and the ledger, for one influencer.</summary>
public class InfluencerEarningsViewModel
{
    public required Entities.Referrals.Ambassador Influencer { get; init; }
    public List<CommissionBalance> Balances { get; init; } = [];
    public List<ReferralWithdrawalRequest> Withdrawals { get; init; } = [];
    public List<ReferralPayout> Payouts { get; init; } = [];
    public List<ReferralConversion> Ledger { get; init; } = [];

    /// <summary>What each ledger line was about, by the link it came through (null = site link or
    /// no longer listed).</summary>
    public Dictionary<(ReferralTargetKind Kind, Guid? Id), string?> Titles { get; init; } = [];

    public long MinimumMinor { get; init; }
    public bool ProfileComplete { get; init; }

    public bool HasOpenRequest(string currency) =>
        Withdrawals.Any(w => w.Currency == currency && w.Status == WithdrawalStatus.Open);

    public bool CanRequest(CommissionBalance balance) =>
        ProfileComplete && balance.OwedMinor >= MinimumMinor && !HasOpenRequest(balance.Currency);
}

public class InfluencerJournalViewModel
{
    public List<JournalPost> Posts { get; init; } = [];
}

/// <summary>"New article": a headline and a category, as in the admin panel; the rest is written on
/// the next screen, where images can be added because the article now exists.</summary>
public class InfluencerStartArticleViewModel
{
    [Required(ErrorMessage = "Influencer.Journal.Error.Title"), StringLength(200)]
    public string Title { get; set; } = "";

    [StringLength(400)]
    public string? Excerpt { get; set; }

    public JournalCategory Category { get; set; } = JournalCategory.Culture;
}

/// <summary>What the writer posts.</summary>
public class InfluencerArticleForm
{
    [StringLength(200)]
    public string Title { get; set; } = "";

    [StringLength(400)]
    public string? Excerpt { get; set; }

    public string? Body { get; set; }

    public JournalCategory Category { get; set; }

    [StringLength(300)]
    public string? CoverImageAlt { get; set; }

    public JournalAuthorDraft ToDraft() => new(Title, Excerpt, Body ?? "", Category, CoverImageAlt);
}

public class InfluencerWriteViewModel
{
    public required JournalPost Post { get; init; }
    public required InfluencerArticleForm Form { get; init; }

    /// <summary>False while Submitted or Published: the page shows the article, not the editor.</summary>
    public bool Editable { get; init; }

    public string? CoverUrl { get; init; }

    /// <summary>The article as it will look (JournalController.Details lets the author preview).</summary>
    public string PreviewUrl { get; init; } = "";

    /// <summary>For the writer's autosave key: a newer local copy than this is offered back.</summary>
    public long UpdatedAtMs { get; init; }
}

public class InfluencerProfileViewModel
{
    public required Entities.Referrals.Ambassador Influencer { get; init; }
    public required InfluencerProfileForm Form { get; init; }
    public string? PhotoUrl { get; init; }
    public IReadOnlyList<InfluencerRequirement> Missing { get; init; } = [];
}
