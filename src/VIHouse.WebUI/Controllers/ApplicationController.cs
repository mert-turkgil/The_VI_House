using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Experiences;
using VIHouse.WebUI.Services;
using VIHouse.WebUI.ViewModels.Applications;
using VIHouse.WebUI.ViewModels.Experiences;

namespace VIHouse.WebUI.Controllers;

[Route("apply")]
public class ApplicationController(IExperienceService experienceService, IApplicationService applicationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? experience, CancellationToken ct)
    {
        var exp = string.IsNullOrWhiteSpace(experience)
            ? null
            : await experienceService.GetPublicDetailBySlugAsync(experience, ct);

        if (exp is null || exp.Visibility != ExperienceVisibility.Public || !CanApply(exp.Status))
        {
            var open = await experienceService.GetPublicListingAsync(
                new ExperienceFilter { Status = ExperienceStatus.ApplicationsOpen, Take = 20 }, ct);
            ViewData["Title"] = "Request Access";
            return View("ChooseExperience", open.Select(ExperienceCardViewModel.FromEntity).ToList());
        }

        var form = BuildForm(exp);
        if (string.IsNullOrWhiteSpace(form.ReferralCode) && Request.Cookies.TryGetValue(ReferralCookie.Name, out var cookieCode))
            form.ReferralCode = cookieCode;

        ViewData["Title"] = "Request Access";
        return View(form);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("application-submit")]
    public async Task<IActionResult> Index(ApplyFormViewModel form, CancellationToken ct)
    {
        var exp = await experienceService.GetPublicDetailBySlugAsync(form.ExperienceSlug, ct);
        if (exp is null || exp.Visibility != ExperienceVisibility.Public || !CanApply(exp.Status))
        {
            ModelState.AddModelError(string.Empty, "This experience is no longer accepting applications.");
        }

        if (!form.AgreeToTerms)
        {
            ModelState.AddModelError(nameof(form.AgreeToTerms), "You must agree to the Terms & Conditions and Privacy Policy to apply.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Request Access";
            if (exp is not null) RefreshExperienceFields(form, exp);
            return View(form);
        }

        var application = new Application
        {
            ExperienceId = exp!.Id,
            FirstName = form.FirstName.Trim(),
            LastName = form.LastName.Trim(),
            Email = form.Email.Trim(),
            Phone = form.Phone,
            Country = form.Country.Trim().ToUpperInvariant(),
            City = form.City,
            CompanyName = form.CompanyName,
            JobTitle = form.JobTitle,
            Industry = form.Industry,
            CompanyStage = form.CompanyStage,
            LinkedInUrl = form.LinkedInUrl,
            WebsiteUrl = form.WebsiteUrl,
            YearsOfExperience = form.YearsOfExperience,
            AnnualRevenueBand = form.AnnualRevenueBand,
            FundingStage = form.FundingStage,
            MotivationStatement = form.MotivationStatement,
            BuildingStatement = form.BuildingStatement,
            ContributionStatement = form.ContributionStatement,
            HowDidYouHear = form.HowDidYouHear,
            ReferralCode = form.ReferralCode,
        };

        await applicationService.SubmitAsync(application, form.AgreeToTerms, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

        ViewData["Title"] = "Application Received";
        return View("Submitted", new SubmittedViewModel { Experience = exp!, ApplicationId = application.Id });
    }

    [HttpGet("status/{id:guid}")]
    public async Task<IActionResult> Status(Guid id, CancellationToken ct)
    {
        var application = await applicationService.GetForAdminAsync(id, ct);
        if (application is null) return NotFound();

        var experience = await experienceService.GetForAdminEditAsync(application.ExperienceId, ct);

        ViewData["Title"] = "Application Status";
        return View(new ApplicationStatusViewModel
        {
            Id = application.Id,
            FirstName = application.FirstName,
            Status = application.Status,
            ExperienceLabel = experience is null ? "—" : $"The VI House — {experience.City}",
            SubmittedAt = application.SubmittedAt,
        });
    }

    private static bool CanApply(ExperienceStatus status) =>
        status is ExperienceStatus.ApplicationsOpen or ExperienceStatus.AlmostFull;

    private static ApplyFormViewModel BuildForm(Experience exp) => new()
    {
        ExperienceId = exp.Id,
        ExperienceSlug = exp.Slug,
        ExperienceTitle = exp.Title,
        ExperienceCity = exp.City,
        ExperienceCountry = exp.Country,
        ExperienceStartAtUtc = exp.StartAtUtc,
        ExperienceEndAtUtc = exp.EndAtUtc,
    };

    private static void RefreshExperienceFields(ApplyFormViewModel form, Experience exp)
    {
        form.ExperienceId = exp.Id;
        form.ExperienceSlug = exp.Slug;
        form.ExperienceTitle = exp.Title;
        form.ExperienceCity = exp.City;
        form.ExperienceCountry = exp.Country;
        form.ExperienceStartAtUtc = exp.StartAtUtc;
        form.ExperienceEndAtUtc = exp.EndAtUtc;
    }
}
