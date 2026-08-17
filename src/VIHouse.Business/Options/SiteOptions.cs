namespace VIHouse.Business.Options;

/// <summary>Non-secret, environment-specific site config — currently just the absolute base URL, needed to build links (e.g. invitation URLs) inside emails, where there's no HttpContext to derive it from.</summary>
public class SiteOptions
{
    public string BaseUrl { get; set; } = "";
}
