using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Users;
using VIHouse.WebUI.ViewModels.Account;

namespace VIHouse.WebUI.Controllers;

/// <summary>The member-facing "Basic Profile" + "Booking" account area (brief §206) — deliberately a
/// plain MVC controller, not part of the Areas/Identity Razor Pages scaffold, which only owns
/// auth mechanics (login/password/2FA), not member-facing content like this.</summary>
[Authorize]
[Route("account")]
public class AccountController(
    UserManager<ApplicationUser> userManager,
    IProfileRepository profiles,
    IBookingRepository bookings,
    IExperienceService experienceService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = CurrentUserId();
        var profile = await profiles.GetByUserIdAsync(userId, ct);
        ViewData["Title"] = "My Profile";
        return View(profile is null ? new ProfileFormViewModel() : ProfileFormViewModel.FromEntity(profile));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileFormViewModel form, CancellationToken ct)
    {
        ViewData["Title"] = "My Profile";
        if (!ModelState.IsValid) return View(form);

        var userId = CurrentUserId();
        var profile = await profiles.GetByUserIdAsync(userId, ct);
        if (profile is null)
        {
            profile = new Profile { UserId = userId };
            ApplyForm(profile, form);
            await profiles.AddAsync(profile, ct);
        }
        else
        {
            ApplyForm(profile, form);
            profiles.Update(profile);
        }

        await profiles.SaveChangesAsync(ct);
        TempData["StatusMessage"] = "Profile saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> Bookings(CancellationToken ct)
    {
        var userId = CurrentUserId();
        var userBookings = await bookings.GetByUserAsync(userId, ct);

        var model = new List<BookingListItemViewModel>();
        foreach (var booking in userBookings.OrderByDescending(b => b.CreatedAt))
        {
            var experience = await experienceService.GetForAdminEditAsync(booking.ExperienceId, ct);
            model.Add(new BookingListItemViewModel
            {
                BookingReference = booking.BookingReference,
                ExperienceLabel = experience is null ? "—" : $"The VI House — {experience.City}",
                StartAtUtc = experience?.StartAtUtc ?? booking.CreatedAt,
                Status = booking.Status,
                AmountMinor = booking.AmountMinor,
                Currency = booking.Currency,
            });
        }

        ViewData["Title"] = "My Bookings";
        return View(model);
    }

    private static void ApplyForm(Profile profile, ProfileFormViewModel form)
    {
        profile.CompanyName = form.CompanyName;
        profile.JobTitle = form.JobTitle;
        profile.Industry = form.Industry;
        profile.Bio = form.Bio;
        profile.LinkedInUrl = form.LinkedInUrl;
        profile.WebsiteUrl = form.WebsiteUrl;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private Guid CurrentUserId() => Guid.Parse(userManager.GetUserId(User)!);
}
