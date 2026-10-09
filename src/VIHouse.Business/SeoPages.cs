namespace VIHouse.Business;

/// <summary>
/// The fixed pages whose search title and description can be written in Admin > Settings > Pages.
/// Detail pages (an experience, a journal post, a session) carry their own SEO fields in their own
/// editors; the homepage uses the settings screen's homepage title and default description.
/// </summary>
public static class SeoPages
{
    /// <param name="TitleKey">Resource key of the built-in title — shown as the placeholder, and (in the
    /// admin's own language) as the page's name in the list.</param>
    /// <param name="DescriptionKey">Resource key of the built-in description, when the page has one.</param>
    public record Page(string Key, string Path, string TitleKey, string? DescriptionKey);

    public static readonly IReadOnlyList<Page> All =
    [
        new("experiences", "/experiences", "Experiences.Title", "Seo.Experiences.Description"),
        new("membership", "/membership", "Membership.Title", "Seo.Membership.Description"),
        new("sessions", "/sessions", "Seminars.Title", "Seo.Sessions.Description"),
        new("journal", "/journal", "Nav.Journal", "Seo.Journal.Description"),
        new("about", "/about", "About.Title", "Seo.About.Description"),
        new("faq", "/faq", "Faq.Title", null),
        new("contact", "/contact", "Contact.Title", "Seo.Contact.Description"),
        new("influencers", "/influencers", "Influencers.Title", "Seo.Influencers.Description"),
        new("terms", "/legal/terms", "Footer.Terms", null),
        new("privacy", "/legal/privacy", "Footer.Privacy", null),
        new("cookies", "/legal/cookies", "Footer.Cookies", null),
        new("refund", "/legal/refund", "Footer.RefundPolicy", null),
    ];

    public static bool IsKnown(string? key) => All.Any(p => p.Key == key);
}
