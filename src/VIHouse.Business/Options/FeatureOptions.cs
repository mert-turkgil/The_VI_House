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
}
