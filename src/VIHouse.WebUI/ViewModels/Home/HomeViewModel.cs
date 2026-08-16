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

public class HeroContent
{
    public string Heading { get; set; } = "Where Ambition Meets Alignment.";
    public string? Subheading { get; set; }
    public string CtaLabel { get; set; } = "Request Access";
    public string CtaUrl { get; set; } = "/apply";
}

public record FeatureItem(string Label, string Description);

public record EcosystemPillar(string Title, string Description);

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
    public string Heading { get; set; } = "";
    public string? Body { get; set; }
    public List<Testimonial> Testimonials { get; set; } = [];
}

public record Testimonial(string Quote, string Author, string Role);
