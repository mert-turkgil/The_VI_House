namespace VIHouse.WebUI.ViewModels.Home;

using VIHouse.WebUI.ViewModels.Experiences;

/// <summary>
/// Everything Views/Home/Index.cshtml needs, assembled once in HomeController so each homepage
/// section is a plain partial reading pre-loaded data — not a ViewComponent making its own round
/// trip per section.
/// </summary>
public class HomeViewModel
{
    public HeroContent Hero { get; set; } = new();
    public string FeatureStripHeading { get; set; } = "Find What Matters To You";
    public List<FeatureItem> Features { get; set; } = [];
    public EcosystemContent Ecosystem { get; set; } = new();
    public List<StatItem> Stats { get; set; } = [];
    public List<ExperienceCardViewModel> Upcoming { get; set; } = [];
    public List<ExperienceCardViewModel> Signature { get; set; } = [];
    public TrustContent Trust { get; set; } = new();
}

/// <summary>
/// The hero carousel. <see cref="Slides"/> holds one entry per visible HeroSlide, already resolved
/// to the reader's culture; the remaining properties are the single-slide fallback assembled from
/// the "hero" content block, used when no slides exist yet. Keeping the fallback means the homepage
/// of a database that predates the carousel still renders its hero rather than a blank band.
/// </summary>
public class HeroContent
{
    public string Heading { get; set; } = "Where Ambition Meets Alignment.";
    public string? Subheading { get; set; }
    public string CtaLabel { get; set; } = "Request Access";
    public string CtaUrl { get; set; } = "/apply";

    public List<HeroSlideViewModel> Slides { get; set; } = [];

    /// <summary>The slides to render — the real ones, or one built from the content block.</summary>
    public IReadOnlyList<HeroSlideViewModel> Panels =>
        Slides.Count > 0
            ? Slides
            : [new HeroSlideViewModel
                {
                    Heading = Heading,
                    Subheading = Subheading,
                    PrimaryCtaLabel = CtaLabel,
                    PrimaryCtaUrl = CtaUrl,
                    SecondaryCtaLabel = "Explore Experiences",
                    SecondaryCtaUrl = "/experiences",
                }];
}

/// <summary>One panel of the hero carousel, flattened to the culture the request resolved to.</summary>
public class HeroSlideViewModel
{
    public string? Eyebrow { get; set; }
    public string Heading { get; set; } = "";
    public string? Subheading { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageAlt { get; set; }
    public string? PrimaryCtaLabel { get; set; }
    public string? PrimaryCtaUrl { get; set; }
    public string? SecondaryCtaLabel { get; set; }
    public string? SecondaryCtaUrl { get; set; }

    public bool HasPrimaryCta => !string.IsNullOrWhiteSpace(PrimaryCtaLabel) && !string.IsNullOrWhiteSpace(PrimaryCtaUrl);
    public bool HasSecondaryCta => !string.IsNullOrWhiteSpace(SecondaryCtaLabel) && !string.IsNullOrWhiteSpace(SecondaryCtaUrl);
}

public record FeatureItem(string Label, string Description);

/// <summary>
/// A card in the ecosystem grid. Everything past Description is optional so a block authored before
/// the cards gained photography still deserialises — a pillar with no ImageUrl renders the crest
/// fallback, and one with no LinkUrl simply has no link.
/// </summary>
public record EcosystemPillar
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string? ImageUrl { get; init; }
    public string? ImageAlt { get; init; }
    public string? LinkLabel { get; init; }
    public string? LinkUrl { get; init; }
}

public class EcosystemContent
{
    public string Heading { get; set; } = "";
    public string? Body { get; set; }
    public string? CtaLabel { get; set; }
    public string? CtaUrl { get; set; }
    public List<EcosystemPillar> Pillars { get; set; } = [];
}

public record StatItem(string Value, string Label);

public class TrustContent
{
    /// <summary>Small caps line above the heading, from the block's Subheading field.</summary>
    public string? Eyebrow { get; set; }

    public string Heading { get; set; } = "";
    public string? Body { get; set; }
    public string? CtaLabel { get; set; }
    public string? CtaUrl { get; set; }

    /// <summary>Heading for the logo strip, e.g. "Trusted by founders from".</summary>
    public string? LogosHeading { get; set; }

    /// <summary>
    /// The companies the strip names. A separate "trust-logos" content block rather than more keys
    /// on this one, because a ContentBlock has exactly one ExtraJson and the testimonials already
    /// use it — and because the strip is the part most likely to be edited on its own.
    /// </summary>
    public List<TrustLogo> Logos { get; set; } = [];

    public List<Testimonial> Testimonials { get; set; } = [];
}

/// <summary>
/// One name in the "trusted by" strip. <see cref="ImageUrl"/> is optional and usually null: a
/// company's logo is its trademark, so the strip renders the name as a wordmark until someone has
/// actually cleared and uploaded the real artwork.
/// </summary>
public record TrustLogo
{
    public string Name { get; init; } = "";
    public string? ImageUrl { get; init; }
}

/// <summary>
/// Written as init properties rather than a positional record because AvatarUrl arrived after the
/// first blocks were authored: a positional record's constructor is what System.Text.Json binds to,
/// and existing ExtraJson has no fourth value to give it.
/// </summary>
public record Testimonial
{
    public string Quote { get; init; } = "";
    public string Author { get; init; } = "";
    public string Role { get; init; } = "";
    public string? AvatarUrl { get; init; }
}
