namespace VIHouse.Business.Options;

/// <summary>Non-secret, environment-specific site config.</summary>
public class SiteOptions
{
    /// <summary>Absolute base URL, needed to build links (e.g. invitation URLs) inside emails, where there's no HttpContext to derive it from.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Inbox that receives the public Contact page's messages. Empty in Production until a real mailbox is set up.</summary>
    public string ContactEmail { get; set; } = "";
}
