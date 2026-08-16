using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.WebUI.Models;
using VIHouse.WebUI.ViewModels.Experiences;
using VIHouse.WebUI.ViewModels.Home;

namespace VIHouse.WebUI.Controllers;

public class HomeController(IExperienceService experienceService, IContentPageRepository contentPages) : Controller
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

        var model = new HomeViewModel
        {
            Hero = new HeroContent
            {
                Heading = hero?.Heading ?? "Where Ambition Meets Alignment.",
                Subheading = hero?.Subheading,
                CtaLabel = hero?.CtaLabel ?? "Request Access",
                CtaUrl = hero?.CtaUrl ?? "/apply",
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
                Heading = trust?.Heading ?? "",
                Body = trust?.BodyText,
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
