using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Content;
using VIHouse.WebUI.Models;
using VIHouse.WebUI.ViewModels.Experiences;
using VIHouse.WebUI.ViewModels.Home;

namespace VIHouse.WebUI.Controllers;

public class HomeController(
    IExperienceService experienceService,
    IContentPageRepository contentPages,
    IHeroSlideRepository heroSlides) : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var page = await contentPages.GetBySlugWithBlocksAsync("home", ct);
        var blocks = page?.Blocks.ToDictionary(b => b.SectionKey) ?? [];
        blocks.TryGetValue("hero", out var hero);
        blocks.TryGetValue("feature-strip", out var featureStrip);
        blocks.TryGetValue("ecosystem", out var ecosystem);
        blocks.TryGetValue("stats", out var stats);
        blocks.TryGetValue("trust", out var trust);
        blocks.TryGetValue("trust-logos", out var trustLogos);

        var model = new HomeViewModel
        {
            Hero = new HeroContent
            {
                Heading = hero?.Heading ?? "Where Ambition Meets Alignment.",
                Subheading = hero?.Subheading,
                CtaLabel = hero?.CtaLabel ?? "Request Access",
                CtaUrl = hero?.CtaUrl ?? "/apply",
                Slides = BuildSlides(await heroSlides.GetVisibleAsync(DateTimeOffset.UtcNow, ct)),
            },
            FeatureStripHeading = featureStrip?.Heading ?? "Find What Matters To You",
            Features = ParseJsonList<FeatureItem>(featureStrip?.ExtraJson),
            Ecosystem = new EcosystemContent
            {
                Heading = ecosystem?.Heading ?? "",
                Body = ecosystem?.BodyText,
                CtaLabel = ecosystem?.CtaLabel,
                CtaUrl = ecosystem?.CtaUrl,
                Pillars = ParseJsonList<EcosystemPillar>(ecosystem?.ExtraJson),
            },
            Stats = ParseJsonList<StatItem>(stats?.ExtraJson),
            Trust = new TrustContent
            {
                Eyebrow = trust?.Subheading,
                Heading = trust?.Heading ?? "",
                Body = trust?.BodyText,
                CtaLabel = trust?.CtaLabel,
                CtaUrl = trust?.CtaUrl,
                LogosHeading = trustLogos?.Heading,
                Logos = ParseJsonList<TrustLogo>(trustLogos?.ExtraJson),
                Testimonials = ParseJsonList<Testimonial>(trust?.ExtraJson),
            },
            Upcoming = (await experienceService.GetUpcomingAsync(6, ct)).Select(ExperienceCardViewModel.FromEntity).ToList(),
            Signature = (await experienceService.GetSignatureAsync(4, ct)).Select(ExperienceCardViewModel.FromEntity).ToList(),
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    /// <summary>
    /// Flattens each slide to the reader's culture. The culture comes from CurrentUICulture, which
    /// UseRequestLocalization has already resolved from the language cookie — the same source the
    /// .resx strings around it use, so a slide can never end up in a different language from the
    /// chrome surrounding it.
    ///
    /// A slide with no translation in any language is skipped rather than rendered headless.
    /// </summary>
    private List<HeroSlideViewModel> BuildSlides(List<HeroSlide> slides)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        var panels = new List<HeroSlideViewModel>(slides.Count);

        foreach (var slide in slides)
        {
            var copy = HeroSlideContent.Resolve(slide, culture);
            if (copy is null) continue;

            panels.Add(new HeroSlideViewModel
            {
                Eyebrow = copy.Eyebrow,
                Heading = copy.Heading,
                Subheading = copy.Subheading,
                ImageUrl = HeroImageUrl(slide),
                ImageAlt = copy.ImageAlt,
                PrimaryCtaLabel = copy.PrimaryCtaLabel,
                PrimaryCtaUrl = slide.PrimaryCtaUrl,
                SecondaryCtaLabel = copy.SecondaryCtaLabel,
                SecondaryCtaUrl = slide.SecondaryCtaUrl,
            });
        }

        return panels;
    }

    /// <summary>
    /// An uploaded image is streamed by MediaController and a pasted one is used as written. The
    /// upload URL carries the slide's UpdatedAt as a version stamp: the path is keyed on the slide
    /// rather than on the file, so replacing the photograph would otherwise leave every visitor
    /// with the previous one until their cache expired.
    /// </summary>
    private string? HeroImageUrl(HeroSlide slide) =>
        slide.ImageStorageKey is null
            ? slide.ImageUrl
            : Url.Action("HeroImage", "Media", new { id = slide.Id, v = (slide.UpdatedAt ?? slide.CreatedAt).ToUnixTimeSeconds() });

    private static List<T> ParseJsonList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
