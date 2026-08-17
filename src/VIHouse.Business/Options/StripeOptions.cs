namespace VIHouse.Business.Options;

/// <summary>
/// Bound from the "Stripe" configuration section. Values come from user-secrets in Development and
/// from environment variables (or a real secrets manager) in Production — never from a committed
/// appsettings.*.json file (brief's own credential-hygiene rule, same as SeedAdmin).
/// </summary>
public class StripeOptions
{
    public string SecretKey { get; set; } = "";
    public string PublishableKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
}
