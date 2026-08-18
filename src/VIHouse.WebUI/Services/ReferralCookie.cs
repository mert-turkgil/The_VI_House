namespace VIHouse.WebUI.Services;

/// <summary>Shared cookie name for referral attribution — set by ReferralController's /r/{code}
/// redirect, read by ApplicationController (pre-fills the apply form) and MembershipController
/// (attaches to the resulting MembershipPayment).</summary>
public static class ReferralCookie
{
    public const string Name = "vih_ref";
}
