namespace VIHouse.Business.Abstract;

/// <summary>
/// Tells the account owner when something on the sign-in side changes. Every method is fire-and-
/// forget from the caller's point of view: the email goes through IEmailService (logged, never
/// thrown), so a mail outage can never block a password change.
/// </summary>
public interface ISecurityAlertService
{
    /// <summary>Password changed, set, or reset.</summary>
    Task PasswordChangedAsync(Guid userId, string? ipAddress, string? userAgent, CancellationToken ct = default);

    /// <summary>Two-step verification switched on, off, or re-paired. The email words it in the
    /// member's language from <paramref name="change"/>.</summary>
    Task TwoFactorChangedAsync(Guid userId, TwoFactorChange change, string? ipAddress, string? userAgent, CancellationToken ct = default);

    /// <summary>
    /// Records a completed sign-in and, when the address has not been seen on the account in the
    /// last thirty days, tells the owner. Regular sign-ins from the usual place stay quiet.
    /// </summary>
    Task RecordSignInAsync(Guid userId, string? ipAddress, string? userAgent, CancellationToken ct = default);
}

public enum TwoFactorChange
{
    Enabled,
    Disabled,
    AuthenticatorReset,
}
