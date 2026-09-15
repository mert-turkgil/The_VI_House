using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Community;
using VIHouse.Entities.Seminars;
using VIHouse.Entities.Users;
using VIHouse.WebUI.Helpers;
using VIHouse.WebUI.ViewModels.Account;
using VIHouse.WebUI.ViewModels.Membership;
using VIHouse.WebUI.ViewModels.Seminars;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// The member-facing account area (brief §206) — deliberately a plain MVC controller, not part of
/// the Areas/Identity Razor Pages scaffold, which only owns auth mechanics (login/password/2FA),
/// not member-facing content like this.
///
/// The area reads differently depending on who is signed in. A member gets a dashboard built
/// around their membership; a guest — someone holding a ticket or a session but no membership —
/// gets the same shell with their bookings in front and an invitation to join; staff get a
/// pointer to the panel. The standing is worked out once, in <see cref="Index"/>, and every
/// other page here is the same for everyone because what it shows is theirs regardless.
/// </summary>
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
    IRepository<CommunityLink> communityLinks,
    IOptions<FeatureOptions> features) : Controller
{
    // --- Dashboard -----------------------------------------------------------------------------

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = CurrentUserId();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Challenge();

        var culture = CultureInfo.CurrentUICulture.Name;
        var now = DateTimeOffset.UtcNow;

        var membership = await membershipService.GetMembershipSummaryAsync(userId, ct);
        var profile = await profiles.GetByUserIdAsync(userId, ct);
        var enrolments = await seminarService.GetEnrolmentsForUserAsync(userId, ct);
        var myBookings = await bookings.GetByUserAsync(userId, ct);
        var isStaff = Roles.AdminRoles.Any(User.IsInRole);

        var model = new AccountDashboardViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? "",
            Membership = membership,
            CanManageBilling = membership is { HasProviderSubscription: true, Membership.ProviderCustomerId: not null },
            MemberNumber = membership is null ? null : MemberNumberFor(membership.Membership.Id),
            ProfileComplete = profile?.IsComplete == true,
            UnreadNotifications = await notificationService.GetUnreadCountAsync(userId, ct),
            TotalSessionCount = enrolments.Count,
            OnDemandSessionCount = enrolments.Count(e => e.Seminar.StartAtUtc is null),
            UpcomingSessions = enrolments
                .Where(e => e.Seminar.StartAtUtc is { } start && (e.Seminar.EndAtUtc ?? start.AddHours(2)) > now)
                .OrderBy(e => e.Seminar.StartAtUtc)
                .Take(3)
                .Select(e => new DashboardSessionItem(
                    e.Seminar.Slug, SeminarContent.Title(e.Seminar, culture), e.Seminar.StartAtUtc,
                    e.Seminar.IsOnline, e.Seminar.Location, e.Enrollment.GrantedVia,
                    !string.IsNullOrWhiteSpace(e.Seminar.MeetingUrl)))
                .ToList(),
            TotalBookingCount = myBookings.Count,
            CommunityEnabled = features.Value.Community,
            DirectoryEnabled = features.Value.MemberDirectory,
        };

        // Standing: membership wins over everything else because it is what the dashboard is built
        // around; a staff account with no membership gets the staff view.
        model.Standing = membership is not null ? AccountStanding.Member
            : isStaff ? AccountStanding.Staff
            : enrolments.Count > 0 || myBookings.Count > 0 ? AccountStanding.Guest
            : AccountStanding.Prospect;

        foreach (var booking in myBookings.Where(b => b.Status is BookingStatus.Confirmed or BookingStatus.Pending))
        {
            var experience = await experienceService.GetForAdminEditAsync(booking.ExperienceId, ct);
            if (experience is null || experience.EndAtUtc < now) continue;

            model.UpcomingBookings.Add(new DashboardBookingItem(
                booking.BookingReference, $"The VI House — {experience.City}", experience.StartAtUtc, booking.Status));
        }
        model.UpcomingBookings = model.UpcomingBookings.OrderBy(b => b.StartAtUtc).Take(3).ToList();

        if (membership is null && features.Value.MembershipSales)
            model.Plans = await PlanCardsAsync(ct);

        ViewData["Title"] = "My Account";
        return View(model);
    }

    // --- Profile ------------------------------------------------------------------------------

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(string? returnUrl, CancellationToken ct)
    {
        var userId = CurrentUserId();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Challenge();

        var profile = await profiles.GetByUserIdAsync(userId, ct);
        var form = ProfileFormViewModel.FromEntity(user, profile);
        form.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        // Sent here from a session they tried to enrol in: say so, rather than leaving them to
        // wonder why the page changed under them.
        if (form.ReturnUrl is not null && profile?.IsComplete != true)
            ViewData["ProfilePrompt"] = true;

        ViewData["Title"] = "My Profile";
        return View(form);
    }

    [HttpPost("profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileFormViewModel form, CancellationToken ct)
    {
        ViewData["Title"] = "My Profile";
        var userId = CurrentUserId();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Challenge();

        // Recovers a browser-autofilled country name ("Cyprus") back to its code ("CY") — see
        // Countries.Normalize and ApplicationController's identical fix for why ModelState.Remove
        // matters here too.
        form.Country = Countries.Normalize(form.Country);
        ModelState.Remove(nameof(form.Country));
        if (!Countries.IsValid(form.Country))
            ModelState.AddModelError(nameof(form.Country), "Choose your country.");
        if (form.EarningsBand is not null && !EarningsBand.IsValid(form.EarningsBand))
            ModelState.AddModelError(nameof(form.EarningsBand), "Choose a range from the list.");

        if (!ModelState.IsValid)
            return View(form);

        user.FirstName = form.FirstName.Trim();
        user.LastName = form.LastName.Trim();
        user.City = form.City?.Trim();
        user.Country = form.Country.Trim().ToUpperInvariant();
        await userManager.UpdateAsync(user);

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

        // Back to wherever they came from — a session page, typically — with no status banner
        // queued: TempData survives one redirect and would otherwise surface on whatever page
        // next happens to read it.
        if (Url.IsLocalUrl(form.ReturnUrl)) return Redirect(form.ReturnUrl!);

        TempData["StatusMessage"] = "Profile saved.";
        return RedirectToAction(nameof(Profile));
    }

    // --- Membership ---------------------------------------------------------------------------

    /// <summary>
    /// The member's own membership: current plan, what happens next (renews / expires / ended),
    /// the billing portal, and every membership they have held. For someone without one it is the
    /// plans, or — while membership is application-only — the route to apply.
    /// </summary>
    [HttpGet("membership")]
    public async Task<IActionResult> Membership(CancellationToken ct)
    {
        var userId = CurrentUserId();
        var current = await membershipService.GetMembershipSummaryAsync(userId, ct);
        var history = await membershipService.GetMembershipHistoryAsync(userId, ct);

        var model = new AccountMembershipViewModel
        {
            Current = current,
            History = history,
            CanManageBilling = current is { HasProviderSubscription: true, Membership.ProviderCustomerId: not null },
            MemberNumber = current is null ? null : MemberNumberFor(current.Membership.Id),
            SalesOpen = features.Value.MembershipSales,
            Plans = current is null && features.Value.MembershipSales ? await PlanCardsAsync(ct) : [],
        };

        ViewData["Title"] = "My Membership";
        return View(model);
    }

    /// <summary>
    /// Hands the member to the provider's hosted billing page — change card, download invoices,
    /// cancel. A POST because it creates a one-time session at the provider on every click; a GET
    /// would do that for every prefetch and link preview too.
    /// </summary>
    [HttpPost("billing")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Billing(CancellationToken ct)
    {
        var returnUrl = Url.Action(nameof(Membership), "Account", null, Request.Scheme)!;
        var portalUrl = await membershipService.CreateBillingPortalUrlAsync(CurrentUserId(), returnUrl, ct);

        if (portalUrl is null)
        {
            TempData["MembershipError"] = "The billing portal isn't available right now. Please contact us and we'll sort it out by hand.";
            return RedirectToAction(nameof(Membership));
        }

        return Redirect(portalUrl);
    }

    [HttpGet("card")]
    public async Task<IActionResult> Card(CancellationToken ct)
    {
        var userId = CurrentUserId();
        var membership = await membershipService.GetCurrentMembershipAsync(userId, ct);
        if (membership is null)
        {
            TempData["MembershipError"] = "You don't have an active membership yet.";
            return RedirectToAction(nameof(Membership));
        }

        var plan = await membershipService.GetPlanAsync(membership.PlanId, ct);
        var user = await userManager.FindByIdAsync(userId.ToString());

        ViewData["Title"] = "My Membership Card";
        return View(new DigitalMemberCardViewModel
        {
            FullName = user is null ? "" : $"{user.FirstName} {user.LastName}",
            PlanName = plan?.Name ?? "Member",
            MemberNumber = MemberNumberFor(membership.Id),
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
        // The community layer is a later chapter of the brief and is switched off at the moment.
        // A 404 rather than the "open to members" message below, because while the flag is off it
        // is not open to members either — telling someone to buy their way in would be a lie.
        if (!features.Value.Community) return NotFound();

        var userId = CurrentUserId();
        var membership = await membershipService.GetCurrentMembershipAsync(userId, ct);
        if (membership is null)
        {
            TempData["MembershipError"] = "The community channels are open to members. Your ticket covers the event itself.";
            return RedirectToAction(nameof(Membership));
        }

        var links = (await communityLinks.FindAsync(l => l.IsActive, ct))
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Label)
            .ToList();

        ViewData["Title"] = "Community";
        return View(links);
    }

    // --- Notifications ------------------------------------------------------------------------

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

    // --- Bookings -----------------------------------------------------------------------------

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

    // --- Sessions (the attendee portal) -----------------------------------------------------

    /// <summary>
    /// The sessions this member has a place on, whichever way they got it — free, covered by their
    /// membership, paid for, or comped. This is the attendee's portal rather than a list: live
    /// sittings that are still to come sit first with their join link, the on-demand library
    /// follows, and past sittings are kept because the recording outlives the date.
    ///
    /// Kept next to Bookings rather than folded into it because an Experience booking is a ticket
    /// to a place on a date, and a Session enrolment is standing access to a body of content;
    /// showing them in one table would flatten that difference.
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions(CancellationToken ct)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        var enrolments = await seminarService.GetEnrolmentsForUserAsync(CurrentUserId(), ct);

        ViewData["Title"] = "My Sessions";
        return View(SessionPortalViewModel.Build(enrolments, culture, DateTimeOffset.UtcNow));
    }

    // --- Helpers ------------------------------------------------------------------------------

    private static void ApplyForm(Profile profile, ProfileFormViewModel form)
    {
        profile.JobTitle = form.JobTitle?.Trim();
        profile.AddressLine1 = form.AddressLine1?.Trim();
        profile.AddressLine2 = form.AddressLine2?.Trim();
        profile.PostalCode = form.PostalCode?.Trim();
        profile.About = form.About.Trim();
        profile.Expectations = form.Expectations.Trim();
        profile.EarningsBand = form.EarningsBand;
        profile.Visibility = form.VisibleInDirectory ? ProfileVisibility.MembersOnly : ProfileVisibility.Private;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Purely presentational — derived from the Membership row's own id, not a separately
    /// stored/sequential field, so there's nothing new to keep in sync.</summary>
    private static string MemberNumberFor(Guid membershipId) =>
        $"VIH-{membershipId:N}".Substring(0, 12).ToUpperInvariant();

    private Guid CurrentUserId() => Guid.Parse(userManager.GetUserId(User)!);

    /// <summary>The plan cards with seat availability, so a full plan is offered as a waitlist
    /// here too rather than a checkout that would only be refused.</summary>
    private async Task<List<MembershipPlanCardViewModel>> PlanCardsAsync(CancellationToken ct)
    {
        var cards = new List<MembershipPlanCardViewModel>();
        foreach (var plan in await membershipService.GetActivePlansAsync(ct))
        {
            var availability = plan.MaxMembers is null ? null : await membershipService.GetPlanAvailabilityAsync(plan.Id, ct);
            cards.Add(MembershipPlanCardViewModel.FromEntity(plan, availability));
        }
        return cards;
    }
}
