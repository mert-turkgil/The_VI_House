using VIHouse.Business.Concrete;
using VIHouse.Entities.Referrals;
using VIHouse.Entities.Seminars;

namespace VIHouse.Business.Abstract;

/// <summary>
/// The influencer programme (code name: ambassadors) — invitations, profiles, referral links, the
/// commission ledger, payouts and withdrawal requests. Errors come back as SharedResource keys (with
/// format arguments where they name something), never as sentences, so each screen says them in the
/// reader's language.
/// </summary>
public interface IAmbassadorService
{
    Task<List<Ambassador>> GetAllAsync(CancellationToken ct = default);
    Task<Ambassador?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Ambassador?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Ambassador?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The existing-account path ("Make influencer" on a user record): the person already has a
    /// login, so the influencer is Active at once. Their profile is filled in afterwards on the
    /// admin page, and they accept the terms from their own area.
    /// </summary>
    Task<AmbassadorCreationResult> CreateForUserAsync(Guid userId, string name, string code, decimal commissionPercent, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// The new-person path: the admin enters everything about the influencer — public profile,
    /// legal identity, billing address and bank — the code is reserved, a Pending row with no account
    /// behind it is opened, and the invitation link is emailed to <see cref="AmbassadorInvite.Email"/>.
    /// The link is never returned. Refused when an account with that email already exists (use
    /// <see cref="CreateForUserAsync"/>) or an invitation is already pending.
    /// </summary>
    Task<AmbassadorCreationResult> InviteAsync(AmbassadorInvite invite, MediaUpload? photo, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>New link, new expiry, same code. The old link stops working. The address and
    /// language can be corrected at the same time — the fix for a mistyped email.</summary>
    Task<AmbassadorCreationResult> ResendInviteAsync(Guid ambassadorId, string? email, string? culture, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Cancels a pending invitation and frees its code. Only for Pending rows.</summary>
    Task<bool> WithdrawInviteAsync(Guid ambassadorId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>What the invitation page needs to know about a token.</summary>
    Task<AmbassadorInviteLookup> GetInviteAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Finishes an invitation: sets the password (or, when the address already has an account with
    /// one, requires that account to be signed in), confirms the email — the link proved it — names
    /// the account after the legal name the admin entered, writes the terms ConsentRecord with the
    /// exact text shown, and makes the influencer Active. The token is spent.
    /// </summary>
    Task<AmbassadorAcceptResult> AcceptInviteAsync(string token, AmbassadorAcceptance form, Guid? signedInUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>For an influencer who never saw the invitation page (made from an existing
    /// account): records their acceptance of the current terms. False if already accepted.</summary>
    Task<bool> AcceptTermsAsync(Guid ambassadorId, Guid userId, string termsText, string? ipAddress, CancellationToken ct = default);

    /// <summary>How long an invitation link works.</summary>
    static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    Task UpdateAsync(Ambassador updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    // --- Profile --------------------------------------------------------------------------------

    /// <summary>Bio, niche and channels — what the House shows of the influencer. Editable by
    /// Marketing on the admin page and by the influencer themselves. Audited.</summary>
    Task<InfluencerSaveResult> UpdateProfileAsync(Guid ambassadorId, InfluencerProfileInput profile, Guid actorUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Legal name, billing address, tax id and bank details — what the House pays against.
    /// Admin-only (Finance / SuperAdmin), audited, and stamps PayoutDetailsUpdatedAt when the bank
    /// details change.</summary>
    Task<InfluencerSaveResult> UpdatePayoutIdentityAsync(Guid ambassadorId, InfluencerPayoutInput payout, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Stores a new profile photo (images only) and deletes the previous file.</summary>
    Task<InfluencerSaveResult> SetPhotoAsync(Guid ambassadorId, MediaUpload upload, Guid actorUserId, string? ipAddress, CancellationToken ct = default);

    Task<InfluencerSaveResult> RemovePhotoAsync(Guid ambassadorId, Guid actorUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>The bytes of the profile photo, or null. Who may see it is the caller's decision.</summary>
    Task<MediaFileInfo?> OpenPhotoAsync(Guid ambassadorId, CancellationToken ct = default);

    // --- Links and visits -------------------------------------------------------------------------

    /// <summary>Fire-and-forget from the /r/{code}[/e|s/{slug}] redirect — a bad/unknown code is simply
    /// not recorded, never an error shown to the visitor. The target says which of the influencer's
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
    /// Everything an influencer can be given a link for right now: every published experience and
    /// every publicly listed session, soonest sitting first. The influencer area and the admin page
    /// render one link + QR per entry (see ReferralController).
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

    // --- The ledger -------------------------------------------------------------------------------

    /// <summary>
    /// Writes one line to the influencer's ledger and tells them — bell notification and email —
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
    /// Records that the House paid the influencer everything owed in <paramref name="currency"/>.
    /// <paramref name="expectedOwedMinor"/> is the balance the admin was looking at: if it has moved
    /// since (a refund, a new sale, a second click) nothing is recorded, so one transfer can never
    /// be booked twice or for a different amount than was shown. An open withdrawal request in that
    /// currency is settled by the same payout.
    /// </summary>
    Task<ReferralPayoutResult> MarkCommissionPaidAsync(Guid ambassadorId, string currency, long expectedOwedMinor,
        string? reference, string? note, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task<List<ReferralPayout>> GetPayoutsAsync(Guid ambassadorId, CancellationToken ct = default);

    /// <summary>Patterns worth a second look before paying out — see <see cref="ReferralFraudSignals"/>.</summary>
    Task<ReferralFraudSignals> GetFraudSignalsAsync(Guid ambassadorId, CancellationToken ct = default);

    /// <summary>Visits to /r/{code} grouped by utm_source (blank = no tag), most first — which
    /// channel the influencer's link is actually being shared on.</summary>
    Task<List<ReferralSourceCount>> GetVisitSourcesAsync(Guid ambassadorId, CancellationToken ct = default);

    /// <summary>The ledger, newest first — what the timelines show.</summary>
    Task<List<ReferralConversion>> GetConversionsAsync(Guid ambassadorId, int take = 50, CancellationToken ct = default);

    // --- Withdrawals ------------------------------------------------------------------------------

    /// <summary>The smallest owed balance that can be requested (Referrals:MinimumWithdrawalMinor).</summary>
    long MinimumWithdrawalMinor { get; }

    /// <summary>
    /// The influencer asks to be paid what is owed in <paramref name="currency"/>. Refused when
    /// their profile is incomplete (see Ambassador.MissingRequirements), the balance is under the
    /// minimum, or a request in that currency is already open. Finance and SuperAdmin are told.
    /// </summary>
    Task<WithdrawalResult> RequestWithdrawalAsync(Guid ambassadorId, string currency, string? note, Guid userId, string? ipAddress, CancellationToken ct = default);

    /// <summary>The influencer takes back an open request. False when it is not theirs or not open.</summary>
    Task<bool> CancelWithdrawalAsync(Guid ambassadorId, Guid requestId, Guid userId, string? ipAddress, CancellationToken ct = default);

    /// <summary>One influencer's requests, newest first.</summary>
    Task<List<ReferralWithdrawalRequest>> GetWithdrawalsAsync(Guid ambassadorId, CancellationToken ct = default);

    /// <summary>Every request for the admin queue — open ones first, oldest open first — with the
    /// influencer and what they are owed in that currency right now.</summary>
    Task<List<WithdrawalQueueItem>> GetWithdrawalQueueAsync(CancellationToken ct = default);

    Task<int> CountOpenWithdrawalsAsync(CancellationToken ct = default);

    /// <summary>
    /// Finance has sent the transfer: records the payout for the whole owed balance (through
    /// <see cref="MarkCommissionPaidAsync"/>, with its stale-balance guard) and marks the request
    /// Paid. The influencer is told.
    /// </summary>
    Task<ReferralPayoutResult> PayWithdrawalAsync(Guid requestId, long expectedOwedMinor, string? reference, string? note, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Turns a request down with a reason the influencer sees. Nothing is paid.</summary>
    Task<bool> RejectWithdrawalAsync(Guid requestId, string reason, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
}

/// <param name="Error">A SharedResource key, formatted with <paramref name="ErrorArgs"/>.</param>
public record AmbassadorCreationResult(bool Success, Ambassador? Ambassador, Guid? UserId, string? Error, object[] ErrorArgs)
{
    public static AmbassadorCreationResult Ok(Ambassador ambassador, Guid? userId) => new(true, ambassador, userId, null, []);
    public static AmbassadorCreationResult Fail(string errorKey, params object[] args) => new(false, null, null, errorKey, args);

    /// <summary>The address already has an account: the "Make influencer" path on that user applies.</summary>
    public static AmbassadorCreationResult ExistingAccount(Guid userId) =>
        new(false, null, userId, "Influencer.Error.ExistingAccount", []);

    /// <summary>Saved (code reserved) but the email did not go out — see Emails &amp; SMS; re-send from the influencer page.</summary>
    public static AmbassadorCreationResult SavedButNotSent(Ambassador ambassador) =>
        new(true, ambassador, null, "Influencer.Error.InviteNotSent", []);
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

/// <summary>One experience or session an influencer can promote with its own link.</summary>
public record ReferralLinkTarget(ReferralTargetKind Kind, Guid Id, string Slug, string Title, string? Subtitle, DateTimeOffset? StartAtUtc);

/// <summary>How one of the influencer's links performed: visits it brought and what came of them.
/// Kind = Site with a null Id is the plain /r/{code} link. A null Title means the experience or
/// session is no longer listed.</summary>
public record ReferralTargetStats(ReferralTargetKind Kind, Guid? Id, string? Title, string? Slug, int Visits, int Applications, int Purchases);

/// <summary>Who clicked a referral link, as far as fraud checks need to know.</summary>
public record ReferralVisitor(string? IpHash, string? UserAgent, Guid? UserId);

/// <summary>Commission in one currency. Earned is net of refunds and voids; Owed can go negative
/// when a refund lands after the payout — that is money to recover or net off the next one.</summary>
public record CommissionBalance(string Currency, long EarnedMinor, long PaidMinor)
{
    public long OwedMinor => EarnedMinor - PaidMinor;
}

/// <param name="Error">A SharedResource key, formatted with <paramref name="ErrorArgs"/>.</param>
public record ReferralPayoutResult(bool Success, ReferralPayout? Payout, string? Error, object[] ErrorArgs)
{
    public static ReferralPayoutResult Ok(ReferralPayout payout) => new(true, payout, null, []);
    public static ReferralPayoutResult Fail(string errorKey, params object[] args) => new(false, null, errorKey, args);
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

    /// <summary>Each warning as a SharedResource key and its format arguments.</summary>
    public List<(string Key, object[] Args)> Warnings
    {
        get
        {
            var w = new List<(string, object[])>();
            if (SelfReferrals > 0) w.Add(("Admin.Influencer.Signal.SelfReferrals", [SelfReferrals]));
            if (OwnVisits > 0) w.Add(("Admin.Influencer.Signal.OwnVisits", [OwnVisits]));
            if (Visits >= 10 && TopNetworkShare >= 0.5) w.Add(("Admin.Influencer.Signal.OneNetwork", [TopNetworkShare.ToString("P0")]));
            if (Visits >= 5 && AutomatedVisits * 5 >= Visits) w.Add(("Admin.Influencer.Signal.Automated", [AutomatedVisits, Visits]));
            return w;
        }
    }
}

/// <summary>One place an influencer publishes, as typed into a form.</summary>
public record InfluencerChannelInput(SocialPlatform Platform, string Url, int? Audience);

/// <summary>What the House shows of an influencer.</summary>
public class InfluencerProfileInput
{
    public string? Bio { get; init; }
    public string? Niche { get; init; }
    public IReadOnlyList<InfluencerChannelInput> Channels { get; init; } = [];
}

/// <summary>What the House pays against: legal identity, billing address, bank.</summary>
public class InfluencerPayoutInput
{
    public string LegalFirstName { get; init; } = "";
    public string LegalLastName { get; init; } = "";
    public string AddressLine1 { get; init; } = "";
    public string? AddressLine2 { get; init; }
    public string City { get; init; } = "";
    public string PostalCode { get; init; } = "";
    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string Country { get; init; } = "";
    public string? TaxId { get; init; }
    public string AccountHolder { get; init; } = "";
    public string Iban { get; init; } = "";
    public string? Bic { get; init; }
}

/// <param name="Errors">SharedResource keys of the form "Influencer.Error.{Field}" — the last
/// segment names the form field it belongs to.</param>
public record InfluencerSaveResult(bool Success, IReadOnlyList<string> Errors)
{
    public static InfluencerSaveResult Ok() => new(true, []);
    public static InfluencerSaveResult Fail(params string[] errorKeys) => new(false, errorKeys);
}

/// <summary>
/// The rules for influencer forms, as error keys of the form "Influencer.Error.{Field}". Public so a
/// form can show every problem at once before it posts; the service applies the same rules again.
/// </summary>
public static class InfluencerValidation
{
    /// <summary>At most this many channels per influencer — a profile, not a link farm.</summary>
    public const int MaxChannels = 8;

    public static List<string> Profile(InfluencerProfileInput profile)
    {
        var errors = new List<string>();
        if (profile.Channels.Count > MaxChannels || profile.Channels.Any(c => !IsChannel(c))) errors.Add("Influencer.Error.Channels");
        if (profile.Bio?.Trim().Length > 1000 || profile.Niche?.Trim().Length > 200) errors.Add("Influencer.Error.Bio");
        return errors;
    }

    public static List<string> Payout(InfluencerPayoutInput payout)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(payout.LegalFirstName) || string.IsNullOrWhiteSpace(payout.LegalLastName)) errors.Add("Influencer.Error.LegalName");
        if (string.IsNullOrWhiteSpace(payout.AddressLine1) || string.IsNullOrWhiteSpace(payout.City) || string.IsNullOrWhiteSpace(payout.PostalCode))
            errors.Add("Influencer.Error.Address");
        if (payout.Country?.Trim() is not { Length: 2 } country || !country.All(char.IsAsciiLetter)) errors.Add("Influencer.Error.Country");
        if (string.IsNullOrWhiteSpace(payout.AccountHolder)) errors.Add("Influencer.Error.AccountHolder");
        if (!Iban.IsValid(payout.Iban)) errors.Add("Influencer.Error.Iban");
        if (!Iban.IsValidBic(payout.Bic)) errors.Add("Influencer.Error.Bic");
        return errors;
    }

    /// <summary>A still image (not a GIF, video or document) within the image size limit.</summary>
    public static bool IsPhoto(string fileName, long length) =>
        MediaPolicy.Classify(fileName) is SeminarMediaKind.Image && length <= MediaPolicy.MaxBytesFor(SeminarMediaKind.Image);

    private static bool IsChannel(InfluencerChannelInput channel) =>
        Uri.TryCreate(channel.Url?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"
        && channel.Url.Trim().Length <= 300 && channel.Audience is null or >= 0;
}

public record AmbassadorInvite(string Email, string Name, string Code, decimal CommissionPercent, string Culture,
    InfluencerProfileInput Profile, InfluencerPayoutInput Payout);

public enum AmbassadorInviteState { Invalid, Expired, Valid }

/// <summary>
/// A token looked up for the invitation page. <see cref="AccountHasPassword"/> means the address
/// already has a login: the page asks them to sign in with it instead of choosing a new password.
/// </summary>
public record AmbassadorInviteLookup(AmbassadorInviteState State, Ambassador? Ambassador, Guid? AccountId, bool AccountHasPassword);

/// <summary>Everything else about the influencer was entered by the House; the invitee only chooses
/// a password and accepts the terms.</summary>
public class AmbassadorAcceptance
{
    /// <summary>Null when the account already has a password (they signed in with it).</summary>
    public string? Password { get; init; }
    /// <summary>The terms exactly as the page showed them, rate included — stored in the ConsentRecord.</summary>
    public string TermsText { get; init; } = "";
    public bool AcceptedTerms { get; init; }
}

public enum AmbassadorAcceptStatus { Activated, Invalid, Expired, SignInRequired, WrongAccount, Rejected }

public record AmbassadorAcceptResult(AmbassadorAcceptStatus Status, Guid? UserId, IReadOnlyList<string> Errors)
{
    public static AmbassadorAcceptResult Of(AmbassadorAcceptStatus status) => new(status, null, []);
    public static AmbassadorAcceptResult Reject(params string[] errors) => new(AmbassadorAcceptStatus.Rejected, null, errors);
}

/// <summary>The version stamped on every acceptance. Bump it when the wording in the
/// AmbassadorInvite.Terms.* resources changes in substance, so each ConsentRecord says which one it
/// was. Any version counts as accepted.</summary>
public static class AmbassadorTerms
{
    public const string Version = "2026-10";
}

/// <param name="Error">A SharedResource key, formatted with <paramref name="ErrorArgs"/>.</param>
public record WithdrawalResult(bool Success, ReferralWithdrawalRequest? Request, string? Error, object[] ErrorArgs)
{
    public static WithdrawalResult Ok(ReferralWithdrawalRequest request) => new(true, request, null, []);
    public static WithdrawalResult Fail(string errorKey, params object[] args) => new(false, null, errorKey, args);
}

/// <summary>A row of the admin withdrawal queue.</summary>
public record WithdrawalQueueItem(ReferralWithdrawalRequest Request, Ambassador Ambassador, long OwedNowMinor);
