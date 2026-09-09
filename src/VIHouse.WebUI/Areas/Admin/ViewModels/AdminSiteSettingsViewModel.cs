using System.ComponentModel.DataAnnotations;
using VIHouse.Business.Options;
using VIHouse.Entities.Settings;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminSiteSettingsViewModel
{
    public AdminSiteSettingsForm Form { get; set; } = default!;

    /// <summary>One tab per site language, written or not — the screen exists as much to show what
    /// is missing as to edit what is there.</summary>
    public List<AdminSiteSettingsTranslationTab> Translations { get; set; } = [];

    public string ActiveCulture { get; set; } = SiteCultures.Default;

    public string? LogoPreviewUrl { get; set; }
    public string? OgImagePreviewUrl { get; set; }
    public bool HasUploadedLogo { get; set; }
    public bool HasUploadedOgImage { get; set; }

    /// <summary>Per-language social image previews, keyed by culture name.</summary>
    public Dictionary<string, string> CultureOgImageUrls { get; set; } = [];

    /// <summary>What the sitemap actually contains right now — pages, and URLs once every language
    /// is counted. Shown rather than described, so a broken listing query is visible here.</summary>
    public int SitemapUrlCount { get; set; }
    public int SitemapPageCount { get; set; }
}

/// <param name="IsWritten">False when no row exists for this language yet — it falls back to English.</param>
public record AdminSiteSettingsTranslationTab(
    SiteCulture Culture, bool IsWritten, AdminSiteSettingsTranslationForm Form);

/// <summary>The facts that are the same in every language.</summary>
public class AdminSiteSettingsForm
{
    [StringLength(300)]
    [Display(Name = "Canonical site address")]
    [Url(ErrorMessage = "That does not look like a full address — include https://")]
    public string? CanonicalBaseUrl { get; set; }

    [Required, StringLength(50)]
    [Display(Name = "Organisation type")]
    public string OrganizationType { get; set; } = "Organization";

    [StringLength(200)]
    [Display(Name = "Registered legal name")]
    public string? LegalName { get; set; }

    [StringLength(20)]
    [Display(Name = "Founded (YYYY or YYYY-MM-DD)")]
    public string? FoundingDate { get; set; }

    [StringLength(500)]
    [Display(Name = "Logo URL")]
    public string? LogoUrl { get; set; }

    [StringLength(500)]
    [Display(Name = "Default social image URL")]
    public string? DefaultOgImageUrl { get; set; }

    [StringLength(500), Display(Name = "Instagram")] public string? InstagramUrl { get; set; }
    [StringLength(500), Display(Name = "LinkedIn")] public string? LinkedInUrl { get; set; }
    [StringLength(500), Display(Name = "X (Twitter)")] public string? XUrl { get; set; }
    [StringLength(500), Display(Name = "Facebook")] public string? FacebookUrl { get; set; }
    [StringLength(500), Display(Name = "YouTube")] public string? YouTubeUrl { get; set; }
    [StringLength(500), Display(Name = "TikTok")] public string? TikTokUrl { get; set; }

    [StringLength(50)]
    [Display(Name = "X handle")]
    public string? TwitterHandle { get; set; }

    [StringLength(320), EmailAddress, Display(Name = "Contact email")] public string? ContactEmail { get; set; }
    [StringLength(40), Display(Name = "Contact phone")] public string? ContactPhone { get; set; }
    [StringLength(300), Display(Name = "Street")] public string? StreetAddress { get; set; }
    [StringLength(120), Display(Name = "City")] public string? AddressLocality { get; set; }
    [StringLength(120), Display(Name = "Region")] public string? AddressRegion { get; set; }
    [StringLength(20), Display(Name = "Postcode")] public string? PostalCode { get; set; }

    [StringLength(2, MinimumLength = 2, ErrorMessage = "Two letters, e.g. GB")]
    [Display(Name = "Country code")]
    public string? AddressCountry { get; set; }

    [StringLength(200)]
    [Display(Name = "Google Search Console token")]
    public string? GoogleSiteVerification { get; set; }

    [StringLength(200)]
    [Display(Name = "Bing Webmaster token")]
    public string? BingSiteVerification { get; set; }

    [Display(Name = "Allow search engines to index this site")]
    public bool AllowIndexing { get; set; } = true;

    [Display(Name = "Extra robots.txt rules")]
    public string? RobotsExtra { get; set; }

    [Display(Name = "Publish /llms.txt")]
    public bool PublishLlmsTxt { get; set; } = true;

    public static AdminSiteSettingsForm FromEntity(SiteSetting s) => new()
    {
        CanonicalBaseUrl = s.CanonicalBaseUrl,
        OrganizationType = s.OrganizationType,
        LegalName = s.LegalName,
        FoundingDate = s.FoundingDate,
        LogoUrl = s.LogoUrl,
        DefaultOgImageUrl = s.DefaultOgImageUrl,
        InstagramUrl = s.InstagramUrl,
        LinkedInUrl = s.LinkedInUrl,
        XUrl = s.XUrl,
        FacebookUrl = s.FacebookUrl,
        YouTubeUrl = s.YouTubeUrl,
        TikTokUrl = s.TikTokUrl,
        TwitterHandle = s.TwitterHandle,
        ContactEmail = s.ContactEmail,
        ContactPhone = s.ContactPhone,
        StreetAddress = s.StreetAddress,
        AddressLocality = s.AddressLocality,
        AddressRegion = s.AddressRegion,
        PostalCode = s.PostalCode,
        AddressCountry = s.AddressCountry,
        GoogleSiteVerification = s.GoogleSiteVerification,
        BingSiteVerification = s.BingSiteVerification,
        AllowIndexing = s.AllowIndexing,
        RobotsExtra = s.RobotsExtra,
        PublishLlmsTxt = s.PublishLlmsTxt,
    };

    /// <summary>
    /// A carrier for the posted values, not the tracked row — the service copies field by field onto
    /// the real one. Ids and storage keys are deliberately absent here, so a hand-crafted post
    /// cannot repoint an image at a file it does not own.
    /// </summary>
    public SiteSetting ToEntity() => new()
    {
        CanonicalBaseUrl = CanonicalBaseUrl,
        OrganizationType = OrganizationType,
        LegalName = LegalName,
        FoundingDate = FoundingDate,
        LogoUrl = LogoUrl,
        DefaultOgImageUrl = DefaultOgImageUrl,
        InstagramUrl = InstagramUrl,
        LinkedInUrl = LinkedInUrl,
        XUrl = XUrl,
        FacebookUrl = FacebookUrl,
        YouTubeUrl = YouTubeUrl,
        TikTokUrl = TikTokUrl,
        TwitterHandle = TwitterHandle,
        ContactEmail = ContactEmail,
        ContactPhone = ContactPhone,
        StreetAddress = StreetAddress,
        AddressLocality = AddressLocality,
        AddressRegion = AddressRegion,
        PostalCode = PostalCode,
        AddressCountry = AddressCountry,
        GoogleSiteVerification = GoogleSiteVerification,
        BingSiteVerification = BingSiteVerification,
        AllowIndexing = AllowIndexing,
        RobotsExtra = RobotsExtra,
        PublishLlmsTxt = PublishLlmsTxt,
    };
}

/// <summary>One language's copy of how the site describes itself.</summary>
public class AdminSiteSettingsTranslationForm
{
    public Guid SiteSettingId { get; set; }

    [Required, StringLength(10)]
    public string Culture { get; set; } = default!;

    [Required, StringLength(120)]
    [Display(Name = "Site name")]
    public string SiteName { get; set; } = default!;

    [Required, StringLength(120)]
    [Display(Name = "Title template")]
    public string TitleTemplate { get; set; } = "{0} — The VI House";

    [StringLength(200)]
    [Display(Name = "Homepage title")]
    public string? HomeTitle { get; set; }

    [StringLength(320)]
    [Display(Name = "Default description")]
    public string? DefaultMetaDescription { get; set; }

    [StringLength(500)]
    [Display(Name = "What the organisation is")]
    public string? OrganizationDescription { get; set; }

    [StringLength(300)]
    [Display(Name = "Social image description")]
    public string? OgImageAlt { get; set; }

    [StringLength(500)]
    [Display(Name = "Social image URL for this language")]
    public string? OgImageUrl { get; set; }

    public static AdminSiteSettingsTranslationForm FromEntity(SiteSettingTranslation t) => new()
    {
        SiteSettingId = t.SiteSettingId,
        Culture = t.Culture,
        SiteName = t.SiteName,
        TitleTemplate = t.TitleTemplate,
        HomeTitle = t.HomeTitle,
        DefaultMetaDescription = t.DefaultMetaDescription,
        OrganizationDescription = t.OrganizationDescription,
        OgImageAlt = t.OgImageAlt,
        OgImageUrl = t.OgImageUrl,
    };

    public static AdminSiteSettingsTranslationForm Empty(Guid settingsId, string culture) => new()
    {
        SiteSettingId = settingsId,
        Culture = culture,
        SiteName = "The VI House",
        TitleTemplate = "{0} — The VI House",
    };

    public SiteSettingTranslation ToEntity() => new()
    {
        SiteSettingId = SiteSettingId,
        Culture = Culture,
        SiteName = SiteName,
        TitleTemplate = TitleTemplate,
        HomeTitle = HomeTitle,
        DefaultMetaDescription = DefaultMetaDescription,
        OrganizationDescription = OrganizationDescription,
        OgImageAlt = OgImageAlt,
        OgImageUrl = OgImageUrl,
    };
}
