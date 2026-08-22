using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Users;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>"Customers" (brief §206) — every registered account (Member + admin-side roles alike),
/// with just enough cross-linked context (profile, applications, bookings) to answer support
/// questions without database access. Role assignment lives here too since promoting/demoting an
/// admin is an operational necessity, not scope creep on top of "view a customer".</summary>
public class AdminUsersController(
    UserManager<ApplicationUser> userManager,
    IApplicationRepository applications,
    IBookingRepository bookings,
    IPaymentRepository payments,
    IMembershipPaymentRepository membershipPayments,
    IProfileRepository profiles,
    IEmailService emailService,
    IAuditLogRepository auditLogs) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var users = await userManager.Users.ToListAsync(ct);
        var allApplications = await applications.GetAllAsync(ct);
        var allBookings = await bookings.GetAllAsync(ct);

        var model = new List<AdminCustomerListItemViewModel>();
        foreach (var user in users.OrderByDescending(u => u.Id))
        {
            var profile = await profiles.GetByUserIdAsync(user.Id, ct);
            model.Add(new AdminCustomerListItemViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? user.UserName ?? "—",
                Roles = (await userManager.GetRolesAsync(user)).ToList(),
                CompanyName = profile?.CompanyName,
                ApplicationCount = allApplications.Count(a => a.UserId == user.Id),
                BookingCount = allBookings.Count(b => b.UserId == user.Id),
            });
        }

        return View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var allApplications = await applications.GetAllAsync(ct);
        var allBookings = await bookings.GetAllAsync(ct);
        var allPayments = await payments.GetAllAsync(ct);

        return View(new AdminCustomerDetailViewModel
        {
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
        });
    }

    /// <summary>
    /// SuperAdmin only. AdminControllerBase authorizes every admin-side role equally, so without
    /// this a Support or Marketing account could grant itself SuperAdmin and take over the panel —
    /// granting roles is a different privilege from using them.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> UpdateRoles(Guid id, string[] roles, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var current = await userManager.GetRolesAsync(user);
        var requested = roles.Intersect(Roles.All).ToList();

        // Refuse to let the last SuperAdmin drop their own SuperAdmin role: nobody would be left
        // able to grant it back, locking role management for everyone permanently.
        var isSelf = string.Equals(userManager.GetUserId(User), id.ToString(), StringComparison.OrdinalIgnoreCase);
        if (isSelf && current.Contains(Roles.SuperAdmin) && !requested.Contains(Roles.SuperAdmin))
        {
            var superAdmins = await userManager.GetUsersInRoleAsync(Roles.SuperAdmin);
            if (superAdmins.Count <= 1)
            {
                TempData["StatusMessage"] = "You're the only SuperAdmin — promote someone else before removing your own access.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        var toAdd = requested.Except(current).ToList();
        var toRemove = current.Except(requested).ToList();

        if (toAdd.Count > 0) await userManager.AddToRolesAsync(user, toAdd);
        if (toRemove.Count > 0) await userManager.RemoveFromRolesAsync(user, toRemove);

        if (toAdd.Count > 0 || toRemove.Count > 0)
        {
            await LogAsync("UserRolesUpdated", id, new { Roles = current }, new { Roles = requested }, ct);
            await auditLogs.SaveChangesAsync(ct);
        }

        TempData["StatusMessage"] = "Roles updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // --- Inviting a new admin -------------------------------------------------------------------

    [HttpGet]
    [Authorize(Roles = Roles.SuperAdmin)]
    public IActionResult Invite() => View(new AdminInviteViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> Invite(AdminInviteViewModel form, CancellationToken ct)
    {
        // Only admin-side roles are grantable here. Intersecting against AdminRoles rather than
        // Roles.All means a posted "Member"/"Ambassador" value is dropped instead of silently
        // applied — this screen creates staff, and a customer-facing role would be a surprise.
        var requestedRoles = (form.Roles ?? []).Intersect(Roles.AdminRoles).ToList();
        if (requestedRoles.Count == 0)
            ModelState.AddModelError(nameof(form.Roles), "Choose at least one admin role.");

        var email = (form.Email ?? "").Trim();
        if (!string.IsNullOrEmpty(email) && await userManager.FindByEmailAsync(email) is not null)
            ModelState.AddModelError(nameof(form.Email),
                "An account already exists for that email address. Grant them admin roles from their customer record instead.");

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
            nameof(ApplicationUser), user.Id, ct);

        await LogAsync("AdminInvited", user.Id, null,
            new { user.Email, Roles = requestedRoles, InvitedBy = invitedBy }, ct);
        await auditLogs.SaveChangesAsync(ct);

        TempData["StatusMessage"] = $"Admin account created for {user.Email}. They've been emailed a setup link.";

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
            AdminUserId = Guid.Parse(userManager.GetUserId(User)!),
            Action = action,
            EntityType = nameof(ApplicationUser),
            EntityId = entityId,
            DataBefore = before is null ? null : JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        }, ct);
}
