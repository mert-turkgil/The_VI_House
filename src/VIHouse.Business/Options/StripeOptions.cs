namespace VIHouse.Business.Options;

/// <summary>
/// Bound from the "Stripe" configuration section. Values come from user-secrets in Development and
/// from the server's own appsettings.Production.json in Production — that file is gitignored and
/// populated by hand on the server itself, so real keys never enter git history (same policy as
/// SeedAdmin's credentials — see appsettings.Production.json / .gitignore).
/// </summary>
public class StripeOptions
{
    public string SecretKey { get; set; } = "";
    public string PublishableKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
}
