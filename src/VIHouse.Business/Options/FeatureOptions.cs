namespace VIHouse.Business.Options;

/// <summary>
/// Switches for the parts of the platform that are built but deliberately not open yet.
///
/// The brief phases this product: the application → approval → payment route (§25) is what launches,
/// while paid membership (§44, §46), the member directory (§38) and the community layer are later
/// chapters. All of that code exists and works; what these flags decide is whether a visitor can
/// reach it.
///
/// A flag hides the *entry point* — the nav link, the page, the admin screen. It is not a security
/// boundary: what a signed-in person may see is decided by their membership, checked in
/// <c>MemberAccess</c>. Turning a flag back on restores a working feature rather than revealing a
/// half-finished one, which is the whole reason for switching off rather than deleting.
///
/// Bound from the "Features" configuration section, like <see cref="SiteOptions"/>. Every flag
/// defaults to false: a feature has to be opened deliberately, and a missing configuration file
/// cannot accidentally put the community on the internet.
/// </summary>
public class FeatureOptions
{
    /// <summary>Sells membership plans on /membership and shows Admin → Membership Plans. Off: the
    /// page explains what membership is and points at the application instead.</summary>
    public bool MembershipSales { get; set; }

    /// <summary>The /members directory and its nav entry.</summary>
    public bool MemberDirectory { get; set; }

    /// <summary>The members' community destinations under /account/community.</summary>
    public bool Community { get; set; }

    /// <summary>
    /// Whether a signed-in account must have two-factor switched on before any authorized page will
    /// render. Enforced in one place — <c>OnboardingRequirementFilter</c>.
    ///
    /// The odd one out in this file, and deliberately so: every other flag defaults to false because
    /// a missing configuration section must not open a feature by accident. This one defaults to
    /// <b>true</b> for exactly the same reason — the safe value is "enforced", so a deployment whose
    /// config is missing or mistyped keeps the panel shut rather than throwing it open. Note that
    /// appsettings.json is committed while both environment files are gitignored, so a fresh
    /// checkout has only the committed default to fall back on.
    ///
    /// Off in Development so the panel can be worked on without pairing an authenticator on every
    /// fresh database. It relaxes the two-factor half only: email confirmation is still required,
    /// which costs nothing because seeded admins are created already confirmed.
    /// </summary>
    public bool RequireTwoFactor { get; set; } = true;
}
