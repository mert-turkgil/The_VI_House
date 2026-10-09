using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VIHouse.Business;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Communication;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Journal;
using VIHouse.Entities.Seminars;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Every figure on the dashboard is a query: applications and bookings from their own tables,
/// everything about money from <see cref="IPaymentReportingService"/> (the unified
/// PaymentTransactions view), so a webhook that moves a payment moves the dashboard on the next
/// load. Each panel is loaded only for the roles that can open the section behind it
/// (AdminSections.RolesFor) — so the page is both relevant to the viewer and cheaper to build.
/// </summary>
[Authorize(Roles = AdminSections.RolesFor.Everyone)]
[Route("admin")]
public class AdminDashboardController(
    IApplicationRepository applications,
    IBookingRepository bookings,
    IPaymentReportingService reporting,
    IExperienceRepository experiences,
    ISeminarRepository seminars,
    ISeminarEnrollmentRepository enrollments,
    IRepository<SeminarTranslation> seminarTranslations,
    IJournalPostRepository journal,
    IEmailLogRepository emailLogs,
    INotifySignupRepository notifySignups,
    IUserDirectory directory,
    IFounderService founders,
    IOptionsSnapshot<SmtpOptions> smtp,
    UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    private bool Can(string rolesCsv) => rolesCsv.Split(',').Any(User.IsInRole);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var me = await userManager.GetUserAsync(User);
        var model = new AdminDashboardViewModel
        {
            FirstName = me?.FirstName ?? "",
            RoleLabels = Roles.AdminRoles.Where(User.IsInRole).ToList(),
            CanSeeApplications = Can(AdminSections.RolesFor.Applications),
            CanSeeBookings = Can(AdminSections.RolesFor.Bookings),
            CanSeeMoney = Can(AdminSections.RolesFor.Money),
        };

        if (model.CanSeeApplications)
        {
            var allApplications = await applications.GetAllAsync(ct);
            model.ApplicationsByStatus = allApplications.GroupBy(a => a.Status).ToDictionary(g => g.Key, g => g.Count());
            model.TotalApplications = allApplications.Count;
            model.PendingApplications = model.ApplicationsByStatus.GetValueOrDefault(ApplicationStatus.Submitted)
                + model.ApplicationsByStatus.GetValueOrDefault(ApplicationStatus.UnderReview);
            var paid = model.ApplicationsByStatus.GetValueOrDefault(ApplicationStatus.Paid);
            model.ConversionToPaidPercent = model.TotalApplications == 0 ? 0 : Math.Round(paid * 100.0 / model.TotalApplications, 1);
        }

        if (model.CanSeeBookings)
        {
            var allBookings = await bookings.GetAllAsync(ct);
            model.TotalBookings = allBookings.Count;
            model.BookingsByStatus = allBookings.GroupBy(b => b.Status).ToDictionary(g => g.Key, g => g.Count());
        }

        if (model.CanSeeMoney)
            model.Payments = await reporting.GetDashboardStatsAsync(ct);

        if (Can(AdminSections.RolesFor.Events))
            model.Events = await BuildEventsAsync(now, ct);

        if (Can(AdminSections.RolesFor.Content))
        {
            var posts = await journal.GetAllWithTranslationsAsync(ct);
            model.Content = new DashboardContentPanel
            {
                Drafts = posts.Count(p => p.Status == JournalPostStatus.Draft),
                Published = posts.Count(p => p.Status == JournalPostStatus.Published),
                RecentDrafts = posts.Where(p => p.Status == JournalPostStatus.Draft)
                    .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt).Take(5)
                    .Select(p => new DashboardArticle(p.Id,
                        p.Translations.FirstOrDefault(t => t.Culture == SiteCultures.Default)?.Title
                            ?? p.Translations.FirstOrDefault()?.Title ?? "(untitled)",
                        p.UpdatedAt ?? p.CreatedAt))
                    .ToList(),
            };
        }

        if (Can(AdminSections.RolesFor.Users))
            model.Users = await directory.GetStatsAsync(now.AddDays(-7), ct);

        if (Can(AdminSections.RolesFor.Communications))
        {
            var since = now.AddDays(-7);
            model.Comms = new DashboardCommsPanel
            {
                FailedEmailsLast7Days = await emailLogs.CountAsync(e => e.Status == EmailStatus.Failed && e.ResentAt == null && e.CreatedAt >= since, ct),
                SentEmailsLast7Days = await emailLogs.CountAsync(e => e.Status == EmailStatus.Sent && e.CreatedAt >= since, ct),
                SmtpProblems = smtp.Value.Problems(),
            };
        }

        if (Can(AdminSections.RolesFor.Marketing))
        {
            var programme = await founders.GetProgrammeAsync(ct);
            model.Marketing = new DashboardMarketingPanel
            {
                LaunchListSignups = await notifySignups.CountAsync(ct),
                Founders = (await userManager.GetUsersInRoleAsync(Roles.Founder)).Count,
                FounderWindowEndsAtUtc = programme.WindowEndsAtUtc,
            };
        }

        return View(model);
    }

    private async Task<DashboardEventsPanel> BuildEventsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var horizon = now.AddDays(60);

        var upcomingExperiences = await experiences.FindAsync(e => e.EndAtUtc >= now && e.StartAtUtc <= horizon && e.Status != ExperienceStatus.Draft, ct);
        var experienceIds = upcomingExperiences.Select(e => e.Id).ToList();
        var experienceBookings = await bookings.FindAsync(b => experienceIds.Contains(b.ExperienceId) && b.Status == BookingStatus.Confirmed, ct);
        var bookedByExperience = experienceBookings.GroupBy(b => b.ExperienceId).ToDictionary(g => g.Key, g => g.Sum(b => b.Quantity));

        var upcomingSeminars = await seminars.FindAsync(s => s.StartAtUtc != null && s.StartAtUtc <= horizon
            && (s.EndAtUtc ?? s.StartAtUtc) >= now.AddHours(-3) && s.Status == SeminarStatus.Published, ct);
        var seminarIds = upcomingSeminars.Select(s => s.Id).ToList();
        var titles = (await seminarTranslations.FindAsync(t => seminarIds.Contains(t.SeminarId), ct))
            .GroupBy(t => t.SeminarId)
            .ToDictionary(g => g.Key, g => (g.FirstOrDefault(t => t.Culture == SiteCultures.Default) ?? g.First()).Title);
        var seminarEnrolments = await enrollments.FindAsync(e => seminarIds.Contains(e.SeminarId) && e.Status == SeminarEnrollmentStatus.Confirmed, ct);
        var enrolledBySeminar = seminarEnrolments.GroupBy(e => e.SeminarId).ToDictionary(g => g.Key, g => g.Count());

        var items = upcomingExperiences
            .Select(e => new DashboardEventItem(e.Id, "Experience", e.Title, e.StartAtUtc, e.Status.ToString(),
                bookedByExperience.GetValueOrDefault(e.Id), !string.IsNullOrWhiteSpace(e.LiveStreamUrl),
                now >= e.StartAtUtc.AddHours(-1) && now <= e.EndAtUtc))
            .Concat(upcomingSeminars.Select(s => new DashboardEventItem(s.Id, "Session", titles.GetValueOrDefault(s.Id) ?? s.Slug,
                s.StartAtUtc, s.Status.ToString(), enrolledBySeminar.GetValueOrDefault(s.Id), !string.IsNullOrWhiteSpace(s.LiveStreamUrl),
                SessionTiming.IsLive(s.StartAtUtc, s.EndAtUtc, now))))
            .OrderBy(i => i.StartAtUtc)
            .ToList();

        return new DashboardEventsPanel
        {
            LiveNow = items.Where(i => i.IsLiveNow).ToList(),
            Upcoming = items.Where(i => !i.IsLiveNow).Take(8).ToList(),
            DraftExperiences = await experiences.CountAsync(e => e.Status == ExperienceStatus.Draft, ct),
            DraftSessions = await seminars.CountAsync(s => s.Status == SeminarStatus.Draft, ct),
        };
    }
}
