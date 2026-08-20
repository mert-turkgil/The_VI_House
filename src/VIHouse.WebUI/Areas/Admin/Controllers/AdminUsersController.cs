using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
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
    IProfileRepository profiles) : AdminControllerBase
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRoles(Guid id, string[] roles, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var current = await userManager.GetRolesAsync(user);
        var requested = roles.Intersect(Roles.All).ToList();

        var toAdd = requested.Except(current).ToList();
        var toRemove = current.Except(requested).ToList();

        if (toAdd.Count > 0) await userManager.AddToRolesAsync(user, toAdd);
        if (toRemove.Count > 0) await userManager.RemoveFromRolesAsync(user, toRemove);

        TempData["StatusMessage"] = "Roles updated.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
