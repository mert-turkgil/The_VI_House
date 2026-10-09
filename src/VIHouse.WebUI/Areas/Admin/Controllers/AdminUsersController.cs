using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Users;
using Microsoft.Extensions.Options;
using VIHouse.Business.Options;
using VIHouse.Entities.Referrals;
using VIHouse.WebUI.Areas.Admin.ViewModels;

using VIHouse.WebUI.Areas.Admin;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>"Customers" (brief §206) — every registered account (Member + admin-side roles alike),
/// with just enough cross-linked context (profile, applications, bookings) to answer support
/// questions without database access. Role assignment lives here too since promoting/demoting an
/// admin is an operational necessity, not scope creep on top of "view a customer".</summary>
[Authorize(Roles = AdminSections.RolesFor.Users)]
[Route("admin/users")]
public class AdminUsersController(
    UserManager<ApplicationUser> userManager,
    IApplicationRepository applications,
    IBookingRepository bookings,
    IPaymentRepository payments,
    IMembershipPaymentRepository membershipPayments,
    IProfileRepository profiles,
    IEmailService emailService,
    IAuditLogRepository auditLogs,
    IMembershipService membershipService,
    IAmbassadorService ambassadorService,
    IUserDirectory directory,
    IFounderService founders,
    ISecurityAlertService securityAlerts,
    INotificationService notifications,
    IOptions<SecurityOptions> security,
    IOptions<SiteOptions> siteOptions,
    IStringLocalizer<SharedResource> loc) : AdminControllerBase
{
    /// <summary>
    /// The owner lock. Every action that changes an account goes through here first; a protected
    /// account (Security:ProtectedAccounts) is refused with a 403 no matter who asks — including
    /// another SuperAdmin — so no staff login can alter the owner's access. Reads are unaffected.
    /// The page hides the forms too; this is the check that holds when someone posts anyway.
    /// </summary>
    private IActionResult? RefuseIfProtected(ApplicationUser user)
    {
        if (!security.Value.IsProtected(user.Email)) return null;
        Status(loc["Admin.Users.Msg.Protected", user.Email ?? ""].Value, isError: true);
        Response.Headers["X-Protected-Account"] = "1";
        return RedirectToAction(nameof(Details), new { id = user.Id });
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, string? role, int page = 1, CancellationToken ct = default)
    {
        // Searched, filtered and paged in the database: the old screen loaded every account,
        // application and booking and asked for each user's roles one at a time.
        var result = await directory.SearchAsync(q, role, page, 25, ct);
        return View(new AdminUserIndexViewModel { Query = q, Role = role, Page = result });
    }

    [HttpGet("{id:guid}")]

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var allApplications = await applications.GetAllAsync(ct);
        var allBookings = await bookings.GetAllAsync(ct);
        var allPayments = await payments.GetAllAsync(ct);
        var ambassador = await ambassadorService.GetByUserIdAsync(id, ct);
        return View(new AdminCustomerDetailViewModel
        {
            IsProtected = security.Value.IsProtected(user.Email),
            Phone = user.PhoneNumber,
            Membership = await membershipService.GetMembershipSummaryAsync(id, ct),
            MembershipHistory = await membershipService.GetMembershipHistoryAsync(id, ct),
            Plans = await membershipService.GetActivePlansAsync(ct),
            Ambassador = ambassador,
            AmbassadorForm = new AdminMakeAmbassadorViewModel
            {
                Name = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))),
                Code = SuggestCode(user),
            },
            UserId = user.Id,
            Email = user.Email ?? user.UserName ?? "—",
            Roles = (await userManager.GetRolesAsync(user)).ToList(),
            Profile = await profiles.GetByUserIdAsync(id, ct),
            Applications = allApplications.Where(a => a.UserId == id).OrderByDescending(a => a.SubmittedAt).ToList(),
            Bookings = allBookings.Where(b => b.UserId == id).OrderByDescending(b => b.CreatedAt).ToList(),
            Payments = allPayments.Where(p => p.UserId == id).OrderByDescending(p => p.CreatedAt).ToList(),
            MembershipPayments = (await membershipPayments.GetAllAsync(ct))
                .Where(p => p.UserId == id).OrderByDescending(p => p.CreatedAt).ToList(),
            MemberStatus = user.MemberStatus,
            TwoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(user),
            EmailConfirmed = user.EmailConfirmed,
            IsLockedOut = await userManager.IsLockedOutAsync(user),
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            LockoutEnd = user.LockoutEnd,
            FounderSince = user.FounderSince,
            EditForm = new AdminEditUserViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Phone = user.PhoneNumber,
                Country = user.Country,
                City = user.City,
            },
        });
    }

    /// <summary>
    /// SuperAdmin only. AdminControllerBase authorizes every admin-side role equally, so without
    /// this a Support or Marketing account could grant itself SuperAdmin and take over the panel —
    /// granting roles is a different privilege from using them.
    /// </summary>
    [HttpPost("{id:guid}/update-roles")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> UpdateRoles(Guid id, string[] roles, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (RefuseIfProtected(user) is { } refused) return refused;

        var current = await userManager.GetRolesAsync(user);

        // Member and Ambassador follow from a record (a membership, a referral profile); the form
        // shows them read-only, and anything posted for them is ignored so the role can never
        // drift from the record. Grant or revoke the membership / ambassador link instead.
        var requested = (roles ?? []).Intersect(Roles.All).Except(Roles.Derived)
            .Concat(current.Intersect(Roles.Derived))
            .Distinct()
            .ToList();

        var toAdd = requested.Except(current).ToList();
        var toRemove = current.Except(requested).ToList();

        if (toAdd.Count == 0 && toRemove.Count == 0)
        {
            TempData["StatusMessage"] = "No changes — the roles were already as ticked.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Never leave the panel without a SuperAdmin: nobody would be left able to grant it back.
        if (toRemove.Contains(Roles.SuperAdmin))
        {
            var superAdmins = await userManager.GetUsersInRoleAsync(Roles.SuperAdmin);
            if (superAdmins.Count <= 1)
            {
                Status(loc["Admin.Users.Msg.LastSuperAdmin"].Value, isError: true);
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        var errors = new List<string>();
        if (toAdd.Count > 0)
        {
            var added = await userManager.AddToRolesAsync(user, toAdd);
            if (!added.Succeeded) errors.AddRange(added.Errors.Select(e => e.Description));
        }
        if (toRemove.Count > 0)
        {
            var removed = await userManager.RemoveFromRolesAsync(user, toRemove);
            if (!removed.Succeeded) errors.AddRange(removed.Errors.Select(e => e.Description));
        }

        // Re-read rather than trusting the plan: what is logged and emailed is what actually stuck.
        var after = await userManager.GetRolesAsync(user);
        var actuallyAdded = after.Except(current).ToList();
        var actuallyRemoved = current.Except(after).ToList();

        if (actuallyAdded.Count > 0 || actuallyRemoved.Count > 0)
        {
            // Rolling the stamp is what makes a removed role stop working within the validation
            // interval (five minutes, Program.cs) instead of living on in the old cookie.
            await userManager.UpdateSecurityStampAsync(user);

            await LogAsync("UserRolesUpdated", id, new { Roles = current }, new { Roles = after, Added = actuallyAdded, Removed = actuallyRemoved }, ct);
            await auditLogs.SaveChangesAsync(ct);

            if (actuallyAdded.Contains(Roles.Founder))
                await founders.MarkGrantedManuallyAsync(user.Id, ct);

            var visibleAdded = actuallyAdded.Where(r => r != Roles.Founder).ToList();
            if ((visibleAdded.Count > 0 || actuallyRemoved.Count > 0) && !string.IsNullOrWhiteSpace(user.Email))
            {
                await emailService.SendAsync(
                    "RoleChanged", user.Email, "Your VI House account access has changed",
                    new RoleChangedEmailModel(user.FirstName, visibleAdded, actuallyRemoved,
                        VIHouse.Business.SiteUrls.Absolute(siteOptions.Value.BaseUrl, VIHouse.Business.SiteUrls.Account)),
                    user.PreferredCulture ?? SiteCultures.Default, nameof(ApplicationUser), user.Id, ct);

                await notifications.CreateForUserAsync(user.Id, VIHouse.Entities.Notifications.NotificationType.AccountUpdate,
                    "Your access has changed",
                    string.Join(" ", new[]
                    {
                        visibleAdded.Count > 0 ? $"Added: {string.Join(", ", visibleAdded)}." : null,
                        actuallyRemoved.Count > 0 ? $"Removed: {string.Join(", ", actuallyRemoved)}." : null,
                    }.Where(x => x is not null)),
                    VIHouse.Business.SiteUrls.Account, ct);
            }
        }

        Status(loc["Admin.Users.Msg.RolesUpdated"].Value);
        TempData["StatusMessage"] = errors.Count > 0
            ? $"Some role changes failed: {string.Join(" ", errors)}"
            : $"Roles updated{(actuallyAdded.Count > 0 ? $" — added {string.Join(", ", actuallyAdded)}" : "")}{(actuallyRemoved.Count > 0 ? $" — removed {string.Join(", ", actuallyRemoved)}" : "")}. {user.Email} has been notified; the change applies within five minutes.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // --- Account controls -------------------------------------------------------------------------
    // SuperAdmin only, like role changes, and refused for a protected account. Each one rolls the
    // security stamp so the target's existing sessions end within the validation interval.

    [HttpPost("{id:guid}/lock")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> Lock(Guid id, string? reason, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (RefuseIfProtected(user) is { } refused) return refused;
        if (string.Equals(userManager.GetUserId(User), id.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            TempData["StatusMessage"] = "You can't lock your own account.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await userManager.UpdateSecurityStampAsync(user);

        await LogAsync("UserLocked", id, null, new { user.Email, Reason = reason, LockedBy = User.Identity?.Name }, ct);
        await auditLogs.SaveChangesAsync(ct);

        TempData["StatusMessage"] = $"{user.Email} is locked: they can't sign in, and any open session ends within five minutes.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/unlock")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (RefuseIfProtected(user) is { } refused) return refused;

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        await LogAsync("UserUnlocked", id, null, new { user.Email, UnlockedBy = User.Identity?.Name }, ct);
        await auditLogs.SaveChangesAsync(ct);

        TempData["StatusMessage"] = $"{user.Email} is unlocked and can sign in again.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/sign-out-everywhere")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> SignOutEverywhere(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (RefuseIfProtected(user) is { } refused) return refused;

        await userManager.UpdateSecurityStampAsync(user);
        await LogAsync("UserSignedOutEverywhere", id, null, new { user.Email, By = User.Identity?.Name }, ct);
        await auditLogs.SaveChangesAsync(ct);

        TempData["StatusMessage"] = $"Every session for {user.Email} ends within five minutes.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>For the member whose confirmation email never arrived, once support has confirmed
    /// the address another way. Open to the whole Users section — getting members in is their job.</summary>
    [HttpPost("{id:guid}/confirm-email")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmEmail(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (RefuseIfProtected(user) is { } refused) return refused;
        if (user.EmailConfirmed)
        {
            TempData["StatusMessage"] = "That address is already confirmed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        user.EmailConfirmed = true;
        var result = await userManager.UpdateAsync(user);
        await LogAsync("UserEmailConfirmedByAdmin", id, null, new { user.Email, By = User.Identity?.Name }, ct);
        await auditLogs.SaveChangesAsync(ct);

        TempData["StatusMessage"] = result.Succeeded
            ? $"{user.Email} is marked as confirmed."
            : $"Could not confirm: {string.Join(" ", result.Errors.Select(e => e.Description))}";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/update-profile")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> UpdateProfile(Guid id, AdminEditUserViewModel form, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (RefuseIfProtected(user) is { } refused) return refused;
        if (!ModelState.IsValid)
        {
            TempData["StatusMessage"] = "Not saved: " + string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Details), new { id });
        }

        var before = new { user.FirstName, user.LastName, user.PhoneNumber, user.Country, user.City };
        user.FirstName = form.FirstName.Trim();
        user.LastName = form.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim();
        user.Country = form.Country?.Trim().ToUpperInvariant() ?? "";
        user.City = string.IsNullOrWhiteSpace(form.City) ? null : form.City.Trim();

        var result = await userManager.UpdateAsync(user);
        await LogAsync("UserProfileUpdatedByAdmin", id, before, new { user.FirstName, user.LastName, user.PhoneNumber, user.Country, user.City }, ct);
        await auditLogs.SaveChangesAsync(ct);

        TempData["StatusMessage"] = result.Succeeded ? "Details saved." : $"Not saved: {string.Join(" ", result.Errors.Select(e => e.Description))}";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Clears an account's authenticator pairing so the next sign-in runs the enrolment flow from
    /// scratch (OnboardingRequirementFilter bounces it to /onboarding, which issues a fresh QR code
    /// and a fresh set of recovery codes).
    ///
    /// The production case is a lost or wiped phone: 2FA is mandatory here, so without this the
    /// only remaining way into an account whose recovery codes have also gone is a database edit.
    /// It is equally what makes the first-login experience rehearsable — reset a test admin and the
    /// next sign-in behaves exactly like a freshly seeded account on a brand-new deployment.
    ///
    /// SuperAdmin only, for the same reason UpdateRoles is: any admin who could strip another
    /// admin's second factor could reduce a colleague's account to password-only and then work on
    /// the password at leisure.
    ///
    /// The security stamp is rolled as part of the reset, which invalidates the target's outstanding
    /// Identity tokens and retires their existing cookie at the next validation pass (five minutes —
    /// see SecurityStampValidatorOptions in Program.cs). Their access to anything that matters ends
    /// sooner than that: OnboardingRequirementFilter re-reads two-factor state from the database on
    /// every authorized request, so the panel shuts on their very next click.
    /// </summary>
    [HttpPost("{id:guid}/reset-two-factor")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> ResetTwoFactor(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (RefuseIfProtected(user) is { } refused) return refused;

        var wasEnabled = await userManager.GetTwoFactorEnabledAsync(user);

        // Disable before resetting the key: SetTwoFactorEnabledAsync throws if the account has no
        // authenticator key configured, and ResetAuthenticatorKeyAsync is what removes it.
        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await userManager.UpdateSecurityStampAsync(user);

        await LogAsync("UserTwoFactorReset", id,
            new { user.Email, TwoFactorEnabled = wasEnabled },
            new { user.Email, TwoFactorEnabled = false, ResetBy = User.Identity?.Name }, ct);
        await auditLogs.SaveChangesAsync(ct);

        Status(loc["Admin.Users.Msg.TwoFactorReset", user.Email ?? ""].Value);
        // The owner hears about it: a reset second factor they did not ask for is exactly the kind
        // of change a security alert exists for.
        await securityAlerts.TwoFactorChangedAsync(user.Id, TwoFactorChange.AuthenticatorReset,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);

        TempData["StatusMessage"] =
            $"Two-factor reset for {user.Email}. The panel is closed to them from their next click, their session ends within " +
            "five minutes, and they'll pair a new authenticator app when they sign in again.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Emails the account owner a one-time link to choose a password — the same "Set up your VI House
    /// account" mail a payment sends. For the member whose setup mail never arrived, went to spam or
    /// expired. The link goes only to the address on the account, never to the admin, so this
    /// cannot be used to get into someone else's account.
    /// </summary>
    [HttpPost("{id:guid}/send-setup-link")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendSetupLink(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (RefuseIfProtected(user) is { } refused) return refused;
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            Status(loc["Admin.Users.Msg.NoEmail"].Value, isError: true);
            return RedirectToAction(nameof(Details), new { id });
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        // The public site, not Request host: in production this panel answers on admin.* and the
        // member's link must open the member-facing reset page.
        var setupUrl = VIHouse.Business.SiteUrls.Absolute(siteOptions.Value.BaseUrl, VIHouse.Business.SiteUrls.ResetPassword(encoded));

        var sent = await emailService.SendAsync(
            "WelcomeSetup", user.Email, "Set up your VI House account",
            new WelcomeSetupEmailModel(user.FirstName, setupUrl, null) { SentByAdmin = true, ValidForHours = 24 },
            user.PreferredCulture ?? SiteCultures.Default,
            nameof(ApplicationUser), user.Id, ct);

        await LogAsync("UserSetupLinkSent", id, null, new { user.Email, Sent = sent, SentBy = User.Identity?.Name }, ct);
        await auditLogs.SaveChangesAsync(ct);

        Status(sent
            ? loc["Admin.Users.Msg.SetupLinkSent", user.Email].Value
            : loc["Admin.Users.Msg.SetupLinkFailed", user.Email].Value, isError: !sent);
        return RedirectToAction(nameof(Details), new { id });
    }

    // --- Membership without a purchase ------------------------------------------------------------
    // Influencers, partners, make-goods: an admin grants the plan, the member sees exactly what a
    // paying member sees, and nothing touches Stripe. SuperAdmin only, like everything else here
    // that changes what an account is entitled to.

    [HttpPost("{id:guid}/grant-membership")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> GrantMembership(Guid id, AdminGrantMembershipViewModel form, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (RefuseIfProtected(user) is { } refused) return refused;
        if (!ModelState.IsValid)
        {
            TempData["StatusMessage"] = "Not granted: " + string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Details), new { id });
        }

        DateTimeOffset? expiresAt = form.ExpiresOn is { } date
            ? new DateTimeOffset(date.ToDateTime(new TimeOnly(23, 59, 59)), TimeSpan.Zero)
            : null;

        var result = await membershipService.GrantComplimentaryAsync(
            id, form.PlanId, expiresAt, form.OverrideCap, form.Note, CurrentAdminId(), Ip(), ct);
        Status(result.Message, isError: !result.Success);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/revoke-membership")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> RevokeMembership(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (RefuseIfProtected(user) is { } refused) return refused;

        var result = await membershipService.RevokeMembershipAsync(id, CurrentAdminId(), Ip(), ct);
        Status(result.Message, isError: !result.Success);
        return RedirectToAction(nameof(Details), new { id });
    }

    // --- Ambassador status ------------------------------------------------------------------------
    // The same record an admin would create under Ambassadors, reached from the person rather
    // than from the code. Only admins hand out referral links; a user cannot ask for one.

    [HttpPost("{id:guid}/make-ambassador")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> MakeAmbassador(Guid id, AdminMakeAmbassadorViewModel form, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || user.Email is null) return NotFound();
        if (RefuseIfProtected(user) is { } refused) return refused;
        if (!ModelState.IsValid)
        {
            TempData["StatusMessage"] = "Not created: " + string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Details), new { id });
        }

        if (await ambassadorService.GetByUserIdAsync(id, ct) is not null)
        {
            Status(loc["Admin.Users.Msg.HasReferralLink"].Value, isError: true);
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await ambassadorService.CreateForUserAsync(
            user.Id, form.Name.Trim(), form.Code.Trim().ToUpperInvariant(), form.CommissionPercent, CurrentAdminId(), Ip(), ct);
        Status(result.Success
            ? loc["Admin.Users.Msg.ReferralCreated", result.Ambassador!.Code].Value
            : result.Error, isError: !result.Success);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/set-ambassador-status")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> SetAmbassadorStatus(Guid id, AmbassadorStatus status, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (RefuseIfProtected(user) is { } refused) return refused;

        var ambassador = await ambassadorService.GetByUserIdAsync(id, ct);
        if (ambassador is null) return NotFound();

        ambassador.Status = status;
        await ambassadorService.UpdateAsync(ambassador, CurrentAdminId(), Ip(), ct);
        Status(loc[status == AmbassadorStatus.Active ? "Admin.Users.Msg.ReferralReactivated" : "Admin.Users.Msg.ReferralPaused"].Value);
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>A starting point for the code — first name, upper-cased, letters only — that the
    /// admin can overwrite. "VI-" prefixes are the convention from the brief (§47: VI-ANTON).</summary>
    private static string SuggestCode(ApplicationUser user)
    {
        var stem = new string((user.FirstName ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return stem.Length == 0 ? "" : $"VI-{stem}";
    }

    // --- Inviting a new admin -------------------------------------------------------------------

    [Authorize(Roles = Roles.SuperAdmin)]
    [HttpGet("invite")]
    public IActionResult Invite() => View(new AdminInviteViewModel());

    [HttpPost("invite")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> Invite(AdminInviteViewModel form, CancellationToken ct)
    {
        // Only admin-side roles are grantable here. Intersecting against AdminRoles rather than
        // Roles.All means a posted "Member"/"Ambassador" value is dropped instead of silently
        // applied — this screen creates staff, and a customer-facing role would be a surprise.
        var requestedRoles = (form.Roles ?? []).Intersect(Roles.AdminRoles).ToList();
        if (requestedRoles.Count == 0)
            ModelState.AddModelError(nameof(form.Roles), loc["Admin.Users.Msg.ChooseRole"].Value);

        var email = (form.Email ?? "").Trim();
        if (!string.IsNullOrEmpty(email) && await userManager.FindByEmailAsync(email) is not null)
            ModelState.AddModelError(nameof(form.Email), loc["Admin.Users.Msg.AccountExists"].Value);

        if (!ModelState.IsValid) return View(form);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            // Unconfirmed on purpose: the onboarding gate makes them prove the address before the
            // panel opens, which also catches a typo here before it becomes a dead account.
            EmailConfirmed = false,
            FirstName = form.FirstName.Trim(),
            LastName = form.LastName.Trim(),
            // Non-nullable on ApplicationUser because members must supply it at signup; a staff
            // account has no such requirement, so blank is stored rather than a made-up country.
            Country = form.Country?.Trim().ToUpperInvariant() ?? "",
            MemberStatus = MemberStatus.Active,
        };

        // Created with no password at all, so an abandoned invite leaves an account nobody can sign
        // in to, rather than one with a credential chosen on their behalf.
        var created = await userManager.CreateAsync(user);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(form);
        }

        await userManager.AddToRolesAsync(user, requestedRoles);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var setupUrl = Url.Page("/Account/ResetPassword", pageHandler: null,
            values: new { area = "Identity", code = encoded }, protocol: Request.Scheme)!;

        var invitedBy = User.Identity?.Name ?? "A VI House administrator";
        var roleSummary = string.Join(", ", requestedRoles);

        await emailService.SendAsync(
            "AdminInvite", user.Email!, "Your VI House admin access",
            new AdminInviteEmailModel(user.FirstName, setupUrl, invitedBy, roleSummary),
            user.PreferredCulture ?? SiteCultures.Default,
            nameof(ApplicationUser), user.Id, ct);

        await LogAsync("AdminInvited", user.Id, null,
            new { user.Email, Roles = requestedRoles, InvitedBy = invitedBy }, ct);
        await auditLogs.SaveChangesAsync(ct);

        Status(loc["Admin.Users.Msg.AdminCreated", user.Email ?? ""].Value);

        // The link is shown once on the confirmation screen as well: transactional email is the
        // weakest step in this flow, and a SuperAdmin who can already mint admins learns nothing
        // new from seeing it.
        return View(new AdminInviteViewModel
        {
            FirstName = "",
            LastName = "",
            Email = "",
            IssuedSetupUrl = setupUrl,
            IssuedEmail = user.Email,
        });
    }

    private Task LogAsync(string action, Guid entityId, object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = CurrentAdminId(),
            Action = action,
            EntityType = nameof(ApplicationUser),
            EntityId = entityId,
            DataBefore = before is null ? null : JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        }, ct);
}
