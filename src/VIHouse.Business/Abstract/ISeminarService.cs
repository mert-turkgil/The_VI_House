using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Seminars;

namespace VIHouse.Business.Abstract;

/// <summary>
/// Everything a seminar does: authoring and translating it in the admin panel, deciding who may
/// read it, and taking money from whoever is not covered by a membership.
///
/// The access decision lives here rather than in the controller on purpose — it is re-derived from
/// the database on every enrolment attempt, so a form that was rendered when a session was free
/// cannot be replayed after the price changed, and a "just enrol me" POST cannot skip the payment
/// branch by omitting a field.
/// </summary>
public interface ISeminarService
{
    // --- Public ---------------------------------------------------------------------------------

    Task<List<Seminar>> GetPublicListingAsync(SeminarFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Published detail by slug. Returns null — not a redirect or a login prompt — when the viewer
    /// is not entitled to see that the seminar exists at all, so a Members-only session is a 404 to
    /// the outside world rather than an advertisement.
    ///
    /// <paramref name="viewerIsStaff"/> lifts both the draft and members-only checks. That is what
    /// makes the admin panel's "View page" button work: an editor holds no Member role, so without
    /// it they would get a 404 previewing the very session they had just written.
    /// </summary>
    Task<Seminar?> GetPublicDetailBySlugAsync(
        string slug, bool viewerIsMember, bool viewerIsStaff = false, CancellationToken ct = default);

    /// <summary>What this viewer may do with this seminar right now. Safe for an anonymous viewer
    /// (pass null).</summary>
    Task<SeminarAccessInfo> GetAccessAsync(Seminar seminar, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// Enrols without a payment — for a free session, or one an active membership already covers.
    /// Re-checks entitlement itself and refuses if the seminar actually needs paying for.
    /// </summary>
    Task<SeminarEnrollmentResult> EnrollAsync(Guid seminarId, Guid userId, CancellationToken ct = default);

    /// <summary>Opens a checkout session for a seminar this member has to pay for.</summary>
    Task<SeminarEnrollmentResult> InitiateCheckoutAsync(
        Guid seminarId, Guid userId, string successUrl, string cancelUrl, CancellationToken ct = default);

    /// <summary>Reads LOCAL state only for the success-page redirect — never trusts the browser's
    /// return from the provider on its own (brief §32). IsConfirmed is false until the webhook has
    /// landed, which the caller should render as "processing", not as a failure.</summary>
    Task<SeminarConfirmationInfo?> GetConfirmationBySessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Processes a signature-verified webhook event. Idempotency comes purely from the enrolment's
    /// own Status, so — exactly like MembershipService — this shares no bookkeeping with
    /// PaymentService's ProcessedWebhookEvent ledger and is safe to call unconditionally alongside it.
    /// </summary>
    Task HandleWebhookEventAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct = default);

    /// <summary>Confirmed enrolments for one member, for their account page.</summary>
    Task<List<Seminar>> GetEnrolledSeminarsAsync(Guid userId, CancellationToken ct = default);

    // --- Admin --- (every mutation is audit-logged) ----------------------------------------------

    Task<List<Seminar>> GetAllForAdminAsync(CancellationToken ct = default);
    Task<Seminar?> GetForAdminEditAsync(Guid id, CancellationToken ct = default);

    /// <summary>Creates the seminar together with its default-culture copy — a seminar with no
    /// title in any language is not a thing the rest of the system can render.</summary>
    Task<SeminarSaveResult> CreateAsync(
        Seminar seminar, SeminarTranslation defaultTranslation, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Core fields only. Translations and media are edited through their own methods, so
    /// saving the schedule can never silently discard a body someone is halfway through writing.</summary>
    Task<SeminarSaveResult> UpdateAsync(Seminar updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Creates or replaces one culture's copy. The body is sanitised here (see EditorHtml).</summary>
    Task<SeminarSaveResult> SaveTranslationAsync(
        Guid seminarId, SeminarTranslation translation, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Removes one culture's copy. Refuses to remove the default culture, which everything
    /// else falls back to.</summary>
    Task<SeminarSaveResult> DeleteTranslationAsync(
        Guid seminarId, string culture, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Publish / unpublish / archive. Publishing is refused while the default-culture copy
    /// is missing or empty.</summary>
    Task<SeminarSaveResult> SetStatusAsync(
        Guid id, SeminarStatus status, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Stores one uploaded asset and attaches it. Validation failures come back as a message, not an
    /// exception.
    ///
    /// <paramref name="isInline"/> is true when the upload came from inside the rich text editor,
    /// which places its own tag in the body — those assets stay in the library (same access check,
    /// same cleanup) but are kept out of the gallery rendered underneath the article.
    /// </summary>
    Task<SeminarMediaResult> AddMediaAsync(
        Guid seminarId, MediaUpload upload, string? title, bool isInline,
        Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task<SeminarSaveResult> RemoveMediaAsync(
        Guid seminarId, Guid mediaId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Chooses which of a seminar's images fronts it. Refuses anything that is not a still
    /// image belonging to this seminar.</summary>
    Task<SeminarSaveResult> SetCoverAsync(
        Guid seminarId, Guid mediaId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Resolves one asset for streaming, having first confirmed this viewer may have it.
    ///
    /// Addressed by asset id alone rather than by slug + id: the ids end up inside every
    /// &lt;img&gt; the rich text editor writes into a body, and those must not break the day an
    /// admin renames the session.
    ///
    /// The cover image follows the seminar's own visibility, since it is on the listing card
    /// anyway; everything else requires a confirmed enrolment. Staff bypass the enrolment check —
    /// they can already see the file in the admin panel, and without this an admin could not
    /// preview the article they just wrote. Returns null for "not allowed" and "no such asset"
    /// alike, so a probe cannot tell the two apart.
    /// </summary>
    Task<MediaFileInfo?> OpenMediaAsync(
        Guid mediaId, Guid? userId, bool viewerIsMember, bool viewerIsStaff, CancellationToken ct = default);

    /// <summary>Permanently removes a seminar, its copy, and its uploaded files. Refuses once
    /// anyone has enrolled — archive it instead, so the people who paid keep their access.</summary>
    Task<SeminarSaveResult> DeleteAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task<List<SeminarEnrollment>> GetEnrollmentsForAdminAsync(Guid seminarId, CancellationToken ct = default);
}

/// <summary>
/// Outcome of a mutation. <c>Error</c> is a SharedResource *key* ("Seminar.Error.SlugTaken"), not a
/// sentence: the message has to reach the reader in the language they chose, and Business has no
/// IStringLocalizer and no business having one. The controllers resolve it — see
/// AdminSeminarsController.Localised.
/// </summary>
public record SeminarSaveResult(bool Success, string? Error, Guid? SeminarId = null)
{
    public static SeminarSaveResult Ok(Guid? id = null) => new(true, null, id);
    public static SeminarSaveResult Fail(string error) => new(false, error, null);
}

/// <summary>
/// Outcome of a media upload. Carries the created row so the rich text editor's upload adapter can
/// build the URL to insert without a second round trip.
/// </summary>
public record SeminarMediaResult(bool Success, string? Error, SeminarMedia? Media)
{
    public static SeminarMediaResult Ok(SeminarMedia media) => new(true, null, media);
    public static SeminarMediaResult Fail(string error) => new(false, error, null);
}

/// <summary>As with <see cref="SeminarSaveResult"/>, <c>Error</c> is a resource key.</summary>
public record SeminarEnrollmentResult(bool Success, SeminarEnrollmentOutcome Outcome, string? CheckoutUrl, string? Error)
{
    public static SeminarEnrollmentResult Enrolled() => new(true, SeminarEnrollmentOutcome.Enrolled, null, null);
    public static SeminarEnrollmentResult Redirect(string url) => new(true, SeminarEnrollmentOutcome.RedirectToPayment, url, null);
    public static SeminarEnrollmentResult Fail(string error) => new(false, SeminarEnrollmentOutcome.Failed, null, error);
}

public enum SeminarEnrollmentOutcome
{
    Enrolled,
    RedirectToPayment,
    Failed,
}

/// <param name="SeatsRemaining">Null when the seminar has no capacity limit.</param>
public record SeminarAccessInfo(
    SeminarAccessOutcome Outcome,
    long PriceMinor,
    string Currency,
    int? SeatsRemaining)
{
    /// <summary>True only when the body and media should actually be rendered.</summary>
    public bool HasAccess => Outcome == SeminarAccessOutcome.Enrolled;

    /// <summary>True when a POST to the enrol endpoint would do something — used to decide whether
    /// to render the button at all.</summary>
    public bool CanEnrolNow => Outcome is SeminarAccessOutcome.FreeToEnrol
        or SeminarAccessOutcome.IncludedInMembership
        or SeminarAccessOutcome.RequiresPayment;
}

public enum SeminarAccessOutcome
{
    /// <summary>Confirmed — show the content.</summary>
    Enrolled,

    /// <summary>Checkout started but the money has not been confirmed yet.</summary>
    PendingPayment,

    /// <summary>No account. Everything else needs one, since an enrolment belongs to somebody.</summary>
    NeedsSignIn,

    /// <summary>Free to anyone who can see it — one click, no payment.</summary>
    FreeToEnrol,

    /// <summary>Priced, but this member's active membership covers it — one click, no payment.</summary>
    IncludedInMembership,

    /// <summary>Priced, and this viewer is not covered — send them to checkout.</summary>
    RequiresPayment,

    /// <summary>Capacity reached.</summary>
    SoldOut,

    /// <summary>Draft or archived — not open to new enrolments.</summary>
    NotOpen,
}

public record SeminarConfirmationInfo(
    bool IsConfirmed, string? SeminarTitle, string? SeminarSlug, long AmountMinor, string Currency);
