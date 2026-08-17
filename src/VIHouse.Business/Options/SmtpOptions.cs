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
}
