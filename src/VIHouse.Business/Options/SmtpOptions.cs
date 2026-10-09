namespace VIHouse.Business.Options;

/// <summary>
/// Bound from the "Smtp" configuration section. Host/Port/FromName/FromEmail/UseSsl aren't secret —
/// they live directly in appsettings.Development.json / appsettings.Production.json and change per
/// environment. Username/Password ARE secret and follow the same rule as everything else credential-
/// shaped in this project: user-secrets in Development, environment variables (Smtp__Username,
/// Smtp__Password) or a real vault in Production — never committed to a config file.
/// </summary>
public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string FromName { get; set; } = "The VI House";
    public string FromEmail { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>Where a reply lands when a recipient hits "Reply" — the staff inbox for issues/refunds, not the sending mailbox.</summary>
    public string ReplyToEmail { get; set; } = "";

    /// <summary>Seconds to wait on the server before giving up on one message.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// What is wrong with these settings, in words an admin can act on — empty when they look
    /// usable. Checked at startup (logged loudly), on the health endpoint and on Admin › Emails,
    /// because a misconfigured mailer otherwise fails silently one email at a time.
    /// </summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Host)) problems.Add("Smtp:Host is empty — no mail server is configured.");
        if (string.IsNullOrWhiteSpace(FromEmail)) problems.Add("Smtp:FromEmail is empty — every message needs a sender address.");
        if (Port <= 0) problems.Add($"Smtp:Port {Port} is not a valid port.");
        if (!string.IsNullOrWhiteSpace(Username) && string.IsNullOrEmpty(Password))
            problems.Add("Smtp:Username is set but Smtp:Password is empty — the server will refuse to sign in.");
        if (string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Host) && !IsLocalHost(Host))
            problems.Add("Smtp:Username is empty — most mail providers (Hostinger included) refuse to relay without signing in.");
        // Hostinger, Google Workspace, Microsoft 365 and most shared hosts only let a mailbox send
        // as itself. A From address that differs from the login is the classic silent failure:
        // "553 sender address rejected: not owned by user".
        if (!string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(FromEmail)
            && Username.Contains('@') && !string.Equals(Username.Trim(), FromEmail.Trim(), StringComparison.OrdinalIgnoreCase))
            problems.Add($"Smtp:FromEmail ({FromEmail}) differs from Smtp:Username ({Username}). Most providers reject mail sent as an address other than the signed-in mailbox — set them to the same mailbox, or add {FromEmail} as an alias of {Username}.");
        return problems;
    }

    /// <summary>True when the essentials (host and sender) are present at all.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromEmail);

    private static bool IsLocalHost(string host) =>
        host is "localhost" or "127.0.0.1" or "::1" || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
}
