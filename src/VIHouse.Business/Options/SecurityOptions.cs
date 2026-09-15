namespace VIHouse.Business.Options;

/// <summary>
/// Bound from the "Security" configuration section.
/// </summary>
public class SecurityOptions
{
    /// <summary>
    /// Accounts no admin may change from the panel — not their roles, second factor, membership,
    /// ambassador status or lockout, and not by a SuperAdmin either. The owner's own account lives
    /// here so a compromised or careless staff login cannot lock the owner out of their own site.
    /// Plain configuration rather than a secret: the list is a policy, and it has to hold in every
    /// environment the same way.
    /// </summary>
    public string[] ProtectedAccounts { get; set; } = [];

    public bool IsProtected(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && ProtectedAccounts.Any(p => string.Equals(p.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
}
