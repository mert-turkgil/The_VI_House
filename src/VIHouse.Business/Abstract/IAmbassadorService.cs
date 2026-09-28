using VIHouse.Entities.Referrals;

namespace VIHouse.Business.Abstract;

public interface IAmbassadorService
{
    Task<List<Ambassador>> GetAllAsync(CancellationToken ct = default);
    Task<Ambassador?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Ambassador?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Ambassador?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Reuses an existing account by email if one exists (mirrors PaymentService.ProvisionMemberAccountAsync), otherwise provisions a new one with a random never-communicated password — the caller builds the password-reset link, same split of responsibility as the checkout success page.</summary>
    Task<AmbassadorCreationResult> CreateAsync(string email, string name, string code, decimal commissionPercent, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task UpdateAsync(Ambassador updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Fire-and-forget from the /r/{code}[/e|s/{slug}] redirect — a bad/unknown code is simply
    /// not recorded, never an error shown to the visitor. The target says which of the ambassador's
    /// links this was (the plain site link, or one scoped to an experience/session).</summary>
    /// <remarks>
    /// Fraud signals ride along: a keyed hash of the IP (never the address itself), the User-Agent,
    /// and the signed-in account if any. A repeat landing from the same network on the same link
    /// within <see cref="RepeatVisitWindow"/> is not counted again, so refreshing — or a script —
    /// cannot inflate the visit count. Returns whether a visit row was written.
    /// </remarks>
    Task<bool> RecordVisitAsync(string code, ReferralTargetKind targetKind, Guid? targetId, string? landingPath,
        string? utmSource, string? utmMedium, string? utmCampaign, string? utmContent,
        ReferralVisitor? visitor = null, CancellationToken ct = default);

    /// <summary>How long a repeat landing from the same network on the same link is folded into the first.</summary>
    static readonly TimeSpan RepeatVisitWindow = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Everything an ambassador can be given a link for right now: every published experience and
    /// every publicly listed session, soonest sitting first. The dashboard and the admin page render
    /// one link + QR per entry (see ReferralController).
    /// </summary>
    Task<List<ReferralLinkTarget>> GetLinkTargetsAsync(CancellationToken ct = default);

    /// <summary>
    /// Aggregate-only stats (brief §49: "customer private data ambassador'a gösterilmemelidir") —
    /// counts and revenue, never the names/emails of who was referred. Counts are computed live
    /// from the referral codes on applications and payments; revenue, commission and the
    /// earned/paid/owed balances come from the ledger (ReferralConversion, net of refunds and
    /// voids) and the payouts, so a rate change never re-prices what was already earned.
    /// </summary>
    Task<AmbassadorStats> GetStatsAsync(Guid ambassadorId, CancellationToken ct = default);

    /// <summary>
    /// Writes one line to the ambassador's ledger and tells them — bell notification and email —
    /// that something happened because of their link. A null, blank or unknown code is a no-op,
    /// as is a repeat for the same source row and kind (the ledger is unique on that), so the
    /// callers in the webhook handlers can call it unconditionally. Never throws: a failure here
    /// must not undo a payment that has already landed.
    /// </summary>
    Task RecordConversionAsync(string? referralCode, ReferralConversionKind kind, string sourceEntityType, Guid sourceEntityId,
        long? amountMinor = null, string? currency = null,
        ReferralTargetKind targetKind = ReferralTargetKind.Site, Guid? targetId = null,
        Guid? buyerUserId = null, string? buyerEmail = null, CancellationToken ct = default);

    /// <summary>
    /// Money went back to the buyer: the ledger line for that purchase gives back its commission —
    /// pro rata for a partial refund, all of it (and the conversion stops counting) for a full one.
    /// <paramref name="refundedMinor"/> is the provider's cumulative refunded amount, so replays and
    /// out-of-order deliveries only ever move it forward. If the line was already paid out, the
    /// reversal shows as a negative balance to recover. No-op when there is no line.
    /// </summary>
    Task ReverseForRefundAsync(string sourceEntityType, Guid sourceEntityId, long refundedMinor, bool full, CancellationToken ct = default);

    /// <summary>An admin strikes one line's commission out (self-referral, fraud). Audited.</summary>
    Task<bool> VoidConversionAsync(Guid ambassadorId, Guid conversionId, string reason, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Records that the House paid the ambassador everything owed in <paramref name="currency"/>.
    /// <paramref name="expectedOwedMinor"/> is the balance the admin was looking at: if it has moved
    /// since (a refund, a new sale, a second click) nothing is recorded, so one transfer can never
    /// be booked twice or for a different amount than was shown.
    /// </summary>
    Task<ReferralPayoutResult> MarkCommissionPaidAsync(Guid ambassadorId, string currency, long expectedOwedMinor,
        string? reference, string? note, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task<List<ReferralPayout>> GetPayoutsAsync(Guid ambassadorId, CancellationToken ct = default);

    /// <summary>Patterns worth a second look before paying out — see <see cref="ReferralFraudSignals"/>.</summary>
    Task<ReferralFraudSignals> GetFraudSignalsAsync(Guid ambassadorId, CancellationToken ct = default);

    /// <summary>Visits to /r/{code} grouped by utm_source (blank = no tag), most first — which
    /// channel the ambassador's link is actually being shared on.</summary>
    Task<List<ReferralSourceCount>> GetVisitSourcesAsync(Guid ambassadorId, CancellationToken ct = default);

    /// <summary>The ledger, newest first — what the dashboard's timeline shows.</summary>
    Task<List<ReferralConversion>> GetConversionsAsync(Guid ambassadorId, int take = 50, CancellationToken ct = default);
}

public record AmbassadorCreationResult(bool Success, Ambassador? Ambassador, Guid? UserId, string? Error)
{
    public static AmbassadorCreationResult Ok(Ambassador ambassador, Guid userId) => new(true, ambassador, userId, null);
    public static AmbassadorCreationResult Fail(string error) => new(false, null, null, error);
}

public record ReferralSourceCount(string? Source, string? Medium, int Visits);

public record AmbassadorStats(
    int Visits,
    int Applications,
    int ApprovedApplications,
    int TicketPurchases,
    int MembershipPurchases,
    int SessionPurchases,
    Dictionary<string, long> RevenueByCurrency,
    Dictionary<string, long> CommissionByCurrency,
    List<ReferralTargetStats> Targets)
{
    /// <summary>Earned, paid and still owed, per currency, from the ledger.</summary>
    public List<CommissionBalance> Balances { get; init; } = [];

    public int Purchases => TicketPurchases + MembershipPurchases + SessionPurchases;
}

/// <summary>One experience or session an ambassador can promote with its own link.</summary>
public record ReferralLinkTarget(ReferralTargetKind Kind, Guid Id, string Slug, string Title, string? Subtitle, DateTimeOffset? StartAtUtc);

/// <summary>How one of the ambassador's links performed: visits it brought and what came of them.
/// Kind = Site with a null Id is the plain /r/{code} link.</summary>
public record ReferralTargetStats(ReferralTargetKind Kind, Guid? Id, string Title, string? Slug, int Visits, int Applications, int Purchases);

/// <summary>Who clicked a referral link, as far as fraud checks need to know.</summary>
public record ReferralVisitor(string? IpHash, string? UserAgent, Guid? UserId);

/// <summary>Commission in one currency. Earned is net of refunds and voids; Owed can go negative
/// when a refund lands after the payout — that is money to recover or net off the next one.</summary>
public record CommissionBalance(string Currency, long EarnedMinor, long PaidMinor)
{
    public long OwedMinor => EarnedMinor - PaidMinor;
}

public record ReferralPayoutResult(bool Success, ReferralPayout? Payout, string? Error)
{
    public static ReferralPayoutResult Ok(ReferralPayout payout) => new(true, payout, null);
    public static ReferralPayoutResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// Signals over the last <see cref="WindowDays"/> days of visits plus the whole ledger. None of
/// them proves anything on its own; together they say "look before you pay".
/// </summary>
public record ReferralFraudSignals(
    int WindowDays,
    int Visits,
    int DistinctNetworks,
    int TopNetworkVisits,
    int AutomatedVisits,
    int OwnVisits,
    int SelfReferrals)
{
    /// <summary>Share of visits from the single busiest network, 0–1.</summary>
    public double TopNetworkShare => Visits == 0 ? 0 : (double)TopNetworkVisits / Visits;

    public List<string> Warnings
    {
        get
        {
            var w = new List<string>();
            if (SelfReferrals > 0) w.Add($"{SelfReferrals} purchase(s) were made by the ambassador's own account. Void the commission unless the House agreed to it.");
            if (OwnVisits > 0) w.Add($"The ambassador clicked their own link {OwnVisits} time(s) while signed in.");
            if (Visits >= 10 && TopNetworkShare >= 0.5) w.Add($"{TopNetworkShare:P0} of visits came from one network — one household, office or script.");
            if (Visits >= 5 && AutomatedVisits * 5 >= Visits) w.Add($"{AutomatedVisits} of {Visits} visits look automated (no browser, or a known bot/script User-Agent).");
            return w;
        }
    }
}
