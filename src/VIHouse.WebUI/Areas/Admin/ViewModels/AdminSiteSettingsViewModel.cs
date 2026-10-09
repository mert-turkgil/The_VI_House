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
    /// <summary>Per-page search titles and descriptions for <see cref="ActiveCulture"/>.</summary>
    public List<AdminPageSeoRow> Pages { get; set; } = [];

    /// <summary>While the launch curtain is up the live sitemap and llms.txt list only /coming-soon.</summary>
    public bool ComingSoonOn { get; set; }

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
    [Display(Name = "Admin.Field.CanonicalSiteAddress")]
    [Url(ErrorMessage = "Admin.Validation.ThatDoesNotLookLikeA")]
    public string? CanonicalBaseUrl { get; set; }

    [Required, StringLength(50)]
    [Display(Name = "Admin.Field.OrganisationType")]
    public string OrganizationType { get; set; } = "Organization";

    [StringLength(200)]
    [Display(Name = "Admin.Field.RegisteredLegalName")]
    public string? LegalName { get; set; }

    [StringLength(20)]
    [Display(Name = "Admin.Field.FoundedYYYYOrYYYYMMDD")]
    public string? FoundingDate { get; set; }

    [StringLength(500)]
    [Display(Name = "Admin.Field.LogoURL")]
    public string? LogoUrl { get; set; }

    [StringLength(500)]
    [Display(Name = "Admin.Field.DefaultSocialImageURL")]
    public string? DefaultOgImageUrl { get; set; }

    [StringLength(500), Display(Name = "Admin.Field.Instagram")] public string? InstagramUrl { get; set; }
    [StringLength(500), Display(Name = "Admin.Field.LinkedIn")] public string? LinkedInUrl { get; set; }
    [StringLength(500), Display(Name = "Admin.Field.XTwitter")] public string? XUrl { get; set; }
    [StringLength(500), Display(Name = "Admin.Field.Facebook")] public string? FacebookUrl { get; set; }
    [StringLength(500), Display(Name = "Admin.Field.YouTube")] public string? YouTubeUrl { get; set; }
    [StringLength(500), Display(Name = "Admin.Field.TikTok")] public string? TikTokUrl { get; set; }

    [StringLength(50)]
    [Display(Name = "Admin.Field.XHandle")]
    public string? TwitterHandle { get; set; }

    [StringLength(320), EmailAddress, Display(Name = "Admin.Field.ContactEmail")] public string? ContactEmail { get; set; }
    [StringLength(320), EmailAddress, Display(Name = "Admin.Field.InfluencerEmail")] public string? InfluencerEmail { get; set; }
    [StringLength(40), Display(Name = "Admin.Field.ContactPhone")] public string? ContactPhone { get; set; }
    [StringLength(300), Display(Name = "Admin.Field.Street")] public string? StreetAddress { get; set; }
    [StringLength(120), Display(Name = "Admin.Field.City")] public string? AddressLocality { get; set; }
    [StringLength(120), Display(Name = "Admin.Field.Region")] public string? AddressRegion { get; set; }
    [StringLength(20), Display(Name = "Admin.Field.Postcode")] public string? PostalCode { get; set; }

    [StringLength(2, MinimumLength = 2, ErrorMessage = "Admin.Validation.TwoLettersEGGB")]
    [Display(Name = "Admin.Field.CountryCode")]
    public string? AddressCountry { get; set; }

    [StringLength(200)]
    [Display(Name = "Admin.Field.GoogleSearchConsoleToken")]
    public string? GoogleSiteVerification { get; set; }

    [StringLength(200)]
    [Display(Name = "Admin.Field.BingWebmasterToken")]
    public string? BingSiteVerification { get; set; }

    [Display(Name = "Admin.Field.AllowSearchEnginesToIndexThis")]
    public bool AllowIndexing { get; set; } = true;

    [Display(Name = "Admin.Field.ExtraRobotsTxtRules")]
    public string? RobotsExtra { get; set; }

    [Display(Name = "Admin.Field.PublishLlmsTxt")]
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
        InfluencerEmail = s.InfluencerEmail,
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
        InfluencerEmail = InfluencerEmail,
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
    [Display(Name = "Admin.Field.SiteName")]
    public string SiteName { get; set; } = default!;

    [Required, StringLength(120)]
    [Display(Name = "Admin.Field.TitleTemplate")]
    public string TitleTemplate { get; set; } = "{0} — The VI House";

    [StringLength(200)]
    [Display(Name = "Admin.Field.HomepageTitle")]
    public string? HomeTitle { get; set; }

    [StringLength(320)]
    [Display(Name = "Admin.Field.DefaultDescription")]
    public string? DefaultMetaDescription { get; set; }

    [StringLength(500)]
    [Display(Name = "Admin.Field.WhatTheOrganisationIs")]
    public string? OrganizationDescription { get; set; }

    [StringLength(4000)]
    [Display(Name = "Admin.Field.NotesForAIAssistants")]
    public string? LlmsNotes { get; set; }

    [StringLength(300)]
    [Display(Name = "Admin.Field.SocialImageDescription")]
    public string? OgImageAlt { get; set; }

    [StringLength(500)]
    [Display(Name = "Admin.Field.SocialImageURLForThisLanguage")]
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
        LlmsNotes = t.LlmsNotes,
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
        LlmsNotes = LlmsNotes,
        OgImageAlt = OgImageAlt,
        OgImageUrl = OgImageUrl,
    };
}

/// <summary>One fixed page's row in Admin > Settings > Pages, for the active language.</summary>
/// <param name="DefaultTitle">What the page shows when the override is empty, in that language.</param>
public record AdminPageSeoRow(string Key, string Label, string Path, string? Title, string? Description,
    string DefaultTitle, string? DefaultDescription);
