namespace VIHouse.Business.Options;

/// <summary>Non-secret, environment-specific site config.</summary>
public class SiteOptions
{
    /// <summary>Absolute base URL, needed to build links (e.g. invitation URLs) inside emails, where there's no HttpContext to derive it from.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Inbox that receives the public Contact page's messages. Empty in Production until a real mailbox is set up.</summary>
    public string ContactEmail { get; set; } = "";

    /// <summary>
    /// The address the public writes to about anything general — joining, experiences, press,
    /// partnerships. Shown on About, Contact and FAQ.
    /// </summary>
    public string ConciergeEmail { get; set; } = "concierge@thevihouse.com";

    /// <summary>
    /// The address for bookings, payments, refunds and account problems. Replies to the House's own
    /// notifications land here too (Smtp:ReplyToEmail). The sending mailbox, info@, is never shown
    /// as a place to write to: nobody reads it.
    /// </summary>
    public string SupportEmail { get; set; } = "support@thevihouse.com";
}
