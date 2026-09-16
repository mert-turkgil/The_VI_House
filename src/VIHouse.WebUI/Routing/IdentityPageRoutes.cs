using VIHouse.Business;

namespace VIHouse.WebUI.Routing;

/// <summary>
/// The public URL of every Identity Razor Page, keyed by the page's ViewEnginePath — the value that
/// stays in RouteValues["page"] and that ComingSoonGate, OnboardingRequirementFilter and the account
/// nav match on, which is why renaming the URL touches none of them. Values come from
/// <see cref="SiteUrls"/> so an email written in the Business layer and the route it points at can
/// never drift apart. Every page under Areas/Identity/Pages must appear here: the convention that
/// reads this map throws at startup for one that does not, rather than let it fall back to
/// /Identity/Account/Whatever.
/// </summary>
public static class IdentityPageRoutes
{
    public static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/Account/Login"] = SiteUrls.Login,
            ["/Account/Logout"] = SiteUrls.Logout,
            ["/Account/LoginWith2fa"] = SiteUrls.LoginVerify,
            ["/Account/LoginWithRecoveryCode"] = SiteUrls.LoginRecoveryCode,
            ["/Account/Lockout"] = SiteUrls.LoginLocked,
            ["/Account/RecoveryHelp"] = SiteUrls.LoginHelp,
            ["/Account/ExternalLogin"] = SiteUrls.LoginExternal,
            ["/Account/AccessDenied"] = SiteUrls.AccessDenied,
            ["/Account/Register"] = SiteUrls.Register,
            ["/Account/RegisterConfirmation"] = SiteUrls.RegisterConfirmation,
            ["/Account/ForgotPassword"] = SiteUrls.ForgotPassword,
            ["/Account/ForgotPasswordConfirmation"] = SiteUrls.ForgotPasswordSent,
            ["/Account/ResetPassword"] = SiteUrls.ResetPasswordPath,
            ["/Account/ResetPasswordConfirmation"] = SiteUrls.ResetPasswordDone,
            ["/Account/ConfirmEmail"] = SiteUrls.ConfirmEmail,
            ["/Account/ConfirmEmailChange"] = SiteUrls.ConfirmEmailChange,
            ["/Account/ResendEmailConfirmation"] = SiteUrls.ConfirmEmailResend,
            ["/Error"] = SiteUrls.IdentityError,

            ["/Account/Manage/Index"] = SiteUrls.Security,
            ["/Account/Manage/ChangePassword"] = SiteUrls.SecurityPassword,
            ["/Account/Manage/SetPassword"] = SiteUrls.SecuritySetPassword,
            ["/Account/Manage/Email"] = SiteUrls.SecurityEmail,
            ["/Account/Manage/TwoFactorAuthentication"] = SiteUrls.SecurityTwoFactor,
            ["/Account/Manage/EnableAuthenticator"] = SiteUrls.SecurityAuthenticator,
            ["/Account/Manage/ResetAuthenticator"] = SiteUrls.SecurityResetAuthenticator,
            ["/Account/Manage/Disable2fa"] = SiteUrls.SecurityDisableTwoFactor,
            ["/Account/Manage/GenerateRecoveryCodes"] = SiteUrls.SecurityRecoveryCodes,
            ["/Account/Manage/ShowRecoveryCodes"] = SiteUrls.SecurityShowRecoveryCodes,
            ["/Account/Manage/ExternalLogins"] = SiteUrls.SecurityConnections,
            ["/Account/Manage/PersonalData"] = SiteUrls.SecurityData,
            ["/Account/Manage/DownloadPersonalData"] = SiteUrls.SecurityDataDownload,
            ["/Account/Manage/DeletePersonalData"] = SiteUrls.SecurityDataDelete,
        };
}
