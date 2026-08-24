using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Community;
using VIHouse.Entities.Users;
using VIHouse.WebUI.ViewModels.Account;
using VIHouse.WebUI.ViewModels.Seminars;

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
    IExperienceService experienceService,
    ISeminarService seminarService,
    IMembershipService membershipService,
    INotificationService notificationService,
    IRepository<CommunityLink> communityLinks) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = CurrentUserId();
        var profile = await profiles.GetByUserIdAsync(userId, ct);
        ViewData["Title"] = "My Profile";
        ViewData["Membership"] = await CurrentMembershipInfoAsync(userId, ct);
        return View(profile is null ? new ProfileFormViewModel() : ProfileFormViewModel.FromEntity(profile));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileFormViewModel form, CancellationToken ct)
    {
        ViewData["Title"] = "My Profile";
        var userId = CurrentUserId();
        if (!ModelState.IsValid)
        {
            ViewData["Membership"] = await CurrentMembershipInfoAsync(userId, ct);
            return View(form);
        }

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

    [HttpGet("card")]
    public async Task<IActionResult> Card(CancellationToken ct)
    {
        var userId = CurrentUserId();
        var membership = await membershipService.GetCurrentMembershipAsync(userId, ct);
        if (membership is null)
        {
            TempData["MembershipError"] = "You don't have an active membership yet.";
            return RedirectToAction("Index", "Membership");
        }

        var plan = await membershipService.GetPlanAsync(membership.PlanId, ct);
        var user = await userManager.FindByIdAsync(userId.ToString());

        ViewData["Title"] = "My Membership Card";
        return View(new DigitalMemberCardViewModel
        {
            FullName = user is null ? "" : $"{user.FirstName} {user.LastName}",
            PlanName = plan?.Name ?? "Member",
            // Purely presentational — derived from the Membership row's own id, not a separately
            // stored/sequential field, so there's nothing new to keep in sync.
            MemberNumber = $"VIH-{membership.Id:N}".Substring(0, 12).ToUpperInvariant(),
            MemberSince = membership.StartAt,
            ExpiresAt = membership.ExpiresAt,
        });
    }

    /// <summary>
    /// The Discord invite and any live broadcast links. Gated on holding an <em>active
    /// membership</em>, not merely on the Member role: buying a ticket to one experience provisions
    /// an account with that role too, and a single-event guest is not entitled to the year-round
    /// community. An invite URL is a bearer credential — anyone holding it can join — so this must
    /// fail closed.
    /// </summary>
    [HttpGet("community")]
    public async Task<IActionResult> Community(CancellationToken ct)
    {
        var userId = CurrentUserId();
        var membership = await membershipService.GetCurrentMembershipAsync(userId, ct);
        if (membership is null)
        {
            TempData["MembershipError"] = "The community channels are open to members. Your ticket covers the event itself.";
            return RedirectToAction("Index", "Membership");
        }

        var links = (await communityLinks.FindAsync(l => l.IsActive, ct))
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Label)
            .ToList();

        ViewData["Title"] = "Community";
        return View(links);
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(CancellationToken ct)
    {
        ViewData["Title"] = "Notifications";
        return View(await notificationService.GetForUserAsync(CurrentUserId(), ct));
    }

    /// <summary>Plain GET, not a POST — clicking a notification both marks it read and takes you to
    /// its destination in one action, no JS/AJAX needed (same "plain link" pattern as the referral
    /// redirect). Ownership-checked inside MarkReadAsync, so this can never mark someone else's
    /// notification read even if the id is guessed.</summary>
    [HttpGet("notifications/open/{id:guid}")]
    public async Task<IActionResult> OpenNotification(Guid id, CancellationToken ct)
    {
        var userId = CurrentUserId();
        var notification = (await notificationService.GetForUserAsync(userId, ct)).FirstOrDefault(n => n.Id == id);
        await notificationService.MarkReadAsync(id, userId, ct);
        return notification?.Link is { } link && Url.IsLocalUrl(link) ? Redirect(link) : RedirectToAction(nameof(Notifications));
    }

    [HttpPost("notifications/mark-all-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllNotificationsRead(string? returnUrl, CancellationToken ct)
    {
        await notificationService.MarkAllReadAsync(CurrentUserId(), ct);
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Notifications));
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

    /// <summary>
    /// The sessions this member has signed up for, whichever way they got in — free, covered by
    /// their membership, or paid for. Kept next to Bookings rather than folded into it because an
    /// Experience booking is a ticket to a place on a date, and a Session enrolment is standing
    /// access to a body of content; showing them in one table would flatten that difference.
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions(CancellationToken ct)
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        var enrolled = await seminarService.GetEnrolledSeminarsAsync(CurrentUserId(), ct);

        ViewData["Title"] = "My Sessions";
        return View(enrolled.Select(s => SeminarCardViewModel.FromEntity(s, culture)).ToList());
    }

    /// <summary>
    /// The ticket for one booking, looked up by its customer-facing reference rather than the
    /// internal id (brief §110). Scoped to the signed-in user's own bookings, so a guessed or shared
    /// reference belonging to someone else returns 404 rather than someone else's ticket.
    /// </summary>
    [HttpGet("bookings/{reference}")]
    public async Task<IActionResult> Ticket(string reference, CancellationToken ct)
    {
        var userId = CurrentUserId();
        var booking = (await bookings.GetByUserAsync(userId, ct))
            .FirstOrDefault(b => string.Equals(b.BookingReference, reference, StringComparison.OrdinalIgnoreCase));

        if (booking is null) return NotFound();

        var experience = await experienceService.GetForAdminEditAsync(booking.ExperienceId, ct);
        var user = await userManager.FindByIdAsync(userId.ToString());
        var ticketType = experience?.TicketTypes?.FirstOrDefault(t => t.Id == booking.TicketTypeId);

        ViewData["Title"] = $"Ticket {booking.BookingReference}";
        return View(new TicketViewModel
        {
            BookingReference = booking.BookingReference,
            HolderName = user is null ? "" : $"{user.FirstName} {user.LastName}",
            ExperienceTitle = experience is null ? "The VI House" : $"The VI House — {experience.City}",
            City = experience?.City ?? "—",
            Country = experience?.Country ?? "",
            StartAtUtc = experience?.StartAtUtc ?? booking.CreatedAt,
            EndAtUtc = experience?.EndAtUtc ?? booking.CreatedAt,
            TicketTypeName = ticketType?.Title,
            Quantity = booking.Quantity,
            AmountMinor = booking.AmountMinor,
            Currency = booking.Currency,
            Status = booking.Status,
            ConfirmedAt = booking.ConfirmedAt,
            HolderIsMember = await membershipService.GetCurrentMembershipAsync(userId, ct) is not null,
        });
    }

    private static void ApplyForm(Profile profile, ProfileFormViewModel form)
    {
        profile.CompanyName = form.CompanyName;
        profile.JobTitle = form.JobTitle;
        profile.Industry = form.Industry;
        profile.Bio = form.Bio;
        profile.LinkedInUrl = form.LinkedInUrl;
        profile.WebsiteUrl = form.WebsiteUrl;
        profile.Interests = form.Interests;
        profile.LookingFor = form.LookingFor;
        profile.CanHelpWith = form.CanHelpWith;
        profile.Visibility = form.VisibleInDirectory ? ProfileVisibility.MembersOnly : ProfileVisibility.Private;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task<AccountMembershipInfo?> CurrentMembershipInfoAsync(Guid userId, CancellationToken ct)
    {
        var membership = await membershipService.GetCurrentMembershipAsync(userId, ct);
        if (membership is null) return null;

        var plan = await membershipService.GetPlanAsync(membership.PlanId, ct);
        return plan is null ? null : new AccountMembershipInfo(plan.Name, membership.ExpiresAt);
    }

    private Guid CurrentUserId() => Guid.Parse(userManager.GetUserId(User)!);
}
