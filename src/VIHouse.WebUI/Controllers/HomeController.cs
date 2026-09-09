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
using VIHouse.WebUI.Helpers;
using Microsoft.Extensions.Localization;

namespace VIHouse.WebUI.Controllers;

public class HomeController(
    IExperienceService experienceService,
    IContentPageRepository contentPages,
    IHeroSlideRepository heroSlides,
    IStringLocalizer<SharedResource> loc) : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Passed as fallbacks, not as the title and description themselves: whatever an admin
        // types into Site & SEO must win, or that screen would appear to do nothing for the one
        // page it matters most on. These are what a fresh install shows until someone fills it in.
        this.SetSeoFallbacks(
            titleFallback: loc["Seo.Home.Title"].Value,
            descriptionFallback: loc["Seo.Home.Description"].Value,
            canonicalPath: "/");

        // The reader's language: used for the CMS blocks below and for the experience cards
        // further down — see ContentBlockContent and ExperienceContent.
        var culture = CultureInfo.CurrentUICulture.Name;

        var page = await contentPages.GetBySlugWithBlocksAsync("home", ct);
        var blocks = page?.Blocks.ToDictionary(b => b.SectionKey) ?? [];
        blocks.TryGetValue("hero", out var hero);
        blocks.TryGetValue("feature-strip", out var featureStrip);
        blocks.TryGetValue("ecosystem", out var ecosystem);
        blocks.TryGetValue("stats", out var stats);
        blocks.TryGetValue("trust", out var trust);
        blocks.TryGetValue("trust-logos", out var trustLogos);

        // Every block field goes through ContentBlockContent rather than being read off the row.
        //
        // This is what makes the homepage below the hero speak the reader's language. The slider
        // was already translated (HeroSlideTranslation), everything under it was not — not because
        // anything was broken, but because ContentBlock had one row per section and nowhere to put
        // the other three languages. Reading .Heading directly here is now a bug, not a shortcut:
        // it would serve English to a Turkish reader with no error anywhere to show for it.
        //
        // ExtraJson goes through the same resolver, which is the half that matters most — the
        // feature strip's labels, the statistic captions and the testimonials all live in there.
        var model = new HomeViewModel
        {
            Hero = new HeroContent
            {
                Heading = ContentBlockContent.Heading(hero, culture) ?? "Where Ambition Meets Alignment.",
                Subheading = ContentBlockContent.Subheading(hero, culture),
                CtaLabel = ContentBlockContent.CtaLabel(hero, culture) ?? "Request Access",
                // Not translated on purpose: a URL is a route, not a sentence. The language
                // prefix is added at render time — see CultureUrlHelper.Localise in _Hero.cshtml.
                //
                // The default points at the experiences that are open for applications rather than
                // at /apply: that page used to be a second, filterless grid of the same cards, and
                // is now a redirect to exactly this URL.
                CtaUrl = hero?.CtaUrl ?? "/experiences?status=ApplicationsOpen",
                Slides = BuildSlides(await heroSlides.GetVisibleAsync(DateTimeOffset.UtcNow, ct)),
            },
            FeatureStripHeading = ContentBlockContent.Heading(featureStrip, culture) ?? "Find What Matters To You",
            Features = WithIcons(
                ParseJsonList<FeatureItem>(ContentBlockContent.ExtraJson(featureStrip, culture)),
                ParseJsonList<FeatureItem>(featureStrip?.ExtraJson)),
            Ecosystem = new EcosystemContent
            {
                Heading = ContentBlockContent.Heading(ecosystem, culture) ?? "",
                Eyebrow = ContentBlockContent.Subheading(ecosystem, culture),
                Body = ContentBlockContent.BodyText(ecosystem, culture),
                CtaLabel = ContentBlockContent.CtaLabel(ecosystem, culture),
                CtaUrl = ecosystem?.CtaUrl,
                Pillars = ParseJsonList<EcosystemPillar>(ContentBlockContent.ExtraJson(ecosystem, culture)),
            },
            Stats = ParseJsonList<StatItem>(ContentBlockContent.ExtraJson(stats, culture)),
            Trust = new TrustContent
            {
                Eyebrow = ContentBlockContent.Subheading(trust, culture),
                Heading = ContentBlockContent.Heading(trust, culture) ?? "",
                Body = ContentBlockContent.BodyText(trust, culture),
                CtaLabel = ContentBlockContent.CtaLabel(trust, culture),
                CtaUrl = trust?.CtaUrl,
                LogosHeading = ContentBlockContent.Heading(trustLogos, culture),
                Logos = ParseJsonList<TrustLogo>(ContentBlockContent.ExtraJson(trustLogos, culture)),
                Testimonials = ParseJsonList<Testimonial>(ContentBlockContent.ExtraJson(trust, culture)),
            },
            Upcoming = (await experienceService.GetUpcomingAsync(6, ct)).Select(e => ExperienceCardViewModel.FromEntity(e, culture)).ToList(),
            Signature = (await experienceService.GetSignatureAsync(4, ct)).Select(e => ExperienceCardViewModel.FromEntity(e, culture)).ToList(),
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

    /// <summary>
    /// Gives every feature a glyph key that does not depend on the language it is written in.
    ///
    /// The icons are picked by key in the view. A translated item should carry its own "icon", but a
    /// translator editing JSON can reasonably drop a field they do not recognise — so anything
    /// missing one borrows from the English item in the same position. Only if that is missing too
    /// does it fall back to the label, which is the original behaviour and correct for English.
    ///
    /// Positional matching is safe here precisely because it is the fallback: the admin screen tells
    /// translators to keep the order, and a mismatched list degrades to a default glyph rather than
    /// to a wrong one.
    /// </summary>
    private static List<FeatureItem> WithIcons(List<FeatureItem> translated, List<FeatureItem> original)
    {
        for (var i = 0; i < translated.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(translated[i].Icon)) continue;

            var fallback = i < original.Count
                ? original[i].Icon ?? original[i].Label
                : translated[i].Label;

            translated[i] = translated[i] with { Icon = fallback };
        }

        return translated;
    }

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
