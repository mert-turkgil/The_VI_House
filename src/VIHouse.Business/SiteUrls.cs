using VIHouse.Business.Options;

namespace VIHouse.Business;

/// <summary>
/// Every site path that is written down somewhere other than a route attribute — in an email, a
/// notification link, a redirect, or the Identity route map — lives here, once. The Business layer
/// cannot reach IUrlHelper, and the Identity pages are Razor Pages whose public URLs are assigned
/// by convention (see WebUI/Routing/IdentityPageRoutes), so a constant is the only thing both sides
/// can agree on. Paths only, never hosts: <see cref="Absolute"/> joins them to Site:BaseUrl.
/// </summary>
public static class SiteUrls
{
    public const string Home = "/";

    // Sign-in and account recovery (Identity area, mapped by IdentityCleanRouteConvention).
    public const string Login = "/login";
    public const string Logout = "/logout";
    public const string LoginVerify = "/login/verify";
    public const string LoginRecoveryCode = "/login/recovery-code";
    public const string LoginLocked = "/login/locked";
    public const string LoginHelp = "/login/help";
    public const string LoginExternal = "/login/external";
    public const string AccessDenied = "/access-denied";
    public const string Register = "/register";
    public const string RegisterConfirmation = "/register/confirmation";
    public const string ForgotPassword = "/forgot-password";
    public const string ForgotPasswordSent = "/forgot-password/sent";
    public const string ResetPasswordPath = "/reset-password";
    public const string ResetPasswordDone = "/reset-password/done";
    public const string ConfirmEmail = "/confirm-email";
    public const string ConfirmEmailChange = "/confirm-email/change";
    public const string ConfirmEmailResend = "/confirm-email/resend";
    public const string IdentityError = "/error/sign-in";

    // Security & login settings (Identity Manage pages).
    public const string Security = "/account/security";
    public const string SecurityPassword = "/account/security/password";
    public const string SecuritySetPassword = "/account/security/set-password";
    public const string SecurityEmail = "/account/security/email";
    public const string SecurityTwoFactor = "/account/security/two-factor";
    public const string SecurityAuthenticator = "/account/security/two-factor/authenticator";
    public const string SecurityResetAuthenticator = "/account/security/two-factor/reset";
    public const string SecurityDisableTwoFactor = "/account/security/two-factor/disable";
    public const string SecurityRecoveryCodes = "/account/security/two-factor/recovery-codes";
    public const string SecurityShowRecoveryCodes = "/account/security/two-factor/recovery-codes/show";
    public const string SecurityConnections = "/account/security/connections";
    public const string SecurityData = "/account/security/data";
    public const string SecurityDataDownload = "/account/security/data/download";
    public const string SecurityDataDelete = "/account/security/data/delete";

    // Member area (AccountController).
    public const string Account = "/account";
    public const string AccountMembership = "/account/membership";
    public const string AccountBookings = "/account/bookings";
    public const string AccountSessions = "/account/sessions";

    /// <summary>What the member's plan includes — free and member-priced sessions and experiences.</summary>
    public const string AccountBenefits = "/account/benefits";
    public const string AccountCard = "/account/card";

    /// <summary>The member's own record of every payment they have made.</summary>
    public const string AccountPayments = "/account/payments";
    public const string AccountNotifications = "/account/notifications";

    // Public pages.
    public const string Experiences = "/experiences";
    public const string Sessions = "/sessions";
    public const string Journal = "/journal";
    public const string Membership = "/membership";
    public const string Join = "/join";
    public const string Apply = "/apply";
    public const string Contact = "/contact";
    /// <summary>The influencer's own area (InfluencerController). /ambassador, its old address, redirects here.</summary>
    public const string Influencer = "/influencer";
    public const string InfluencerJournal = "/influencer/journal";
    public const string InfluencerEarnings = "/influencer/earnings";
    public const string InfluencerProfile = "/influencer/profile";
    /// <summary>The public "Become an influencer" page (InfluencersController).</summary>
    public const string Influencers = "/influencers";
    public const string Admin = "/admin";

    /// <summary>The page that takes an address off the launch list — linked from every launch email.
    /// The signup's own id is the key: an unguessable Guid, so no one can remove anybody else.</summary>
    public static string LeaveLaunchList(Guid signupId) => $"/coming-soon/leave/{signupId}";

    /// <summary>A path in a given language: /experiences stays as it is for English and becomes
    /// /de/experiences for German. For links in mail, where there is no request to take it from.</summary>
    public static string InCulture(string path, string? culture) =>
        SiteCultures.ToUrlCode(culture) is { } code ? $"/{code}{(path == "/" ? "" : path)}" : path;

    public static string LoginReturningTo(string returnUrl) => $"{Login}?returnUrl={Uri.EscapeDataString(returnUrl)}";
    public static string ResetPassword(string code) => $"{ResetPasswordPath}?code={Uri.EscapeDataString(code)}";
    public static string Booking(string reference) => $"{AccountBookings}/{reference}";
    public static string Experience(string slug) => $"{Experiences}/{slug}";
    public static string Session(string slug) => $"{Sessions}/{slug}";
    public static string JournalPost(string slug) => $"{Journal}/{slug}";
    /// <summary>"Add to calendar" — the event as an .ics file (ExperiencesController/SeminarsController.Calendar).</summary>
    public static string ExperienceCalendar(string slug) => $"{Experiences}/{slug}/calendar.ics";
    public static string SessionCalendar(string slug) => $"{Sessions}/{slug}/calendar.ics";
    public static string Invitation(string code) => $"/invitation/{code}";
    public static string JoinResume(string code) => $"{Join}/resume/{code}";

    // Referral links — one per thing an influencer promotes (see ReferralController).
    public static string Referral(string code) => $"/r/{code}";
    /// <summary>Where an invited influencer accepts (AmbassadorInviteController). The token is the key.</summary>
    public static string InfluencerInvite(string token) => $"{Influencer}/invite/{token}";
    public static string InfluencerJournalPost(Guid postId) => $"{InfluencerJournal}/{postId}";
    /// <summary>The profile photo (MediaController.InfluencerPhoto). The stamp, taken from the
    /// storage key, changes with every new photo, so the URL can be cached hard.</summary>
    public static string InfluencerPhoto(Guid ambassadorId, string storageKey) =>
        $"/media/influencer/{ambassadorId}?v={Concrete.Text.Sha256Hex(storageKey)[..8]}";
    public static string ReferralExperience(string code, string slug) => $"/r/{code}/e/{slug}";
    public static string ReferralSession(string code, string slug) => $"/r/{code}/s/{slug}";

    /// <summary>Joins Site:BaseUrl and a path from this class without doubling or losing the slash.</summary>
    public static string Absolute(string baseUrl, string path) =>
        baseUrl.TrimEnd('/') + (path.StartsWith('/') ? path : "/" + path);
}
