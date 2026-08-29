namespace VIHouse.Business.Options;

/// <summary>
/// Bound from the "Sms" configuration section.
///
/// Deliberately a configured HTTP call rather than a provider SDK. Every gateway worth using exposes
/// the same shape — a form POST carrying a destination, a body and a sender id — so one class covers
/// Twilio, Vonage, NetGSM and İletimerkezi alike, and changing provider is a config edit rather than
/// a package reference and a rebuild. Only the field *names* differ, which is what ToField/BodyField/
/// FromField and ExtraFields are for.
///
/// Endpoint, From and the field names aren't secret and live in appsettings. Username/Password ARE,
/// and follow the same rule as everything else credential-shaped in this project: user-secrets in
/// Development, environment variables (Sms__Username, Sms__Password) or a real vault in Production —
/// never committed. See SmtpOptions.
/// </summary>
public class SmsOptions
{
    /// <summary>Off by default. Nothing is texted until someone deliberately turns this on with a
    /// gateway behind it, so a fresh deployment sends email only rather than throwing on every
    /// approval.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The gateway's send URL, complete. Twilio's carries the account id in the path, so paste it
    /// whole: https://api.twilio.com/2010-04-01/Accounts/ACxxxxxxxx/Messages.json
    /// </summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Sender id — the number or alphanumeric name the message appears to come from. Left
    /// empty, the field is omitted entirely, which is what gateways that infer it from the account
    /// expect.</summary>
    public string From { get; set; } = "";

    /// <summary>"Basic" (Twilio: account sid + auth token) or "None" for gateways that authenticate
    /// with api-key form fields instead — put those in <see cref="ExtraFields"/>.</summary>
    public string AuthScheme { get; set; } = "Basic";

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    public string ToField { get; set; } = "To";
    public string BodyField { get; set; } = "Body";
    public string FromField { get; set; } = "From";

    /// <summary>Anything else the provider requires as a form field: api keys, encoding flags, a
    /// message type. Sent verbatim.</summary>
    public Dictionary<string, string> ExtraFields { get; set; } = [];

    /// <summary>
    /// Dialling code for numbers typed without one, e.g. "+90". Applicants write their number the way
    /// they say it out loud, and a gateway needs E.164. Left empty, a national number is skipped and
    /// logged rather than guessed at — a payment link sent to the same digits in the wrong country is
    /// worse than one not sent.
    /// </summary>
    public string DefaultCountryCode { get; set; } = "";

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(Endpoint);
}
