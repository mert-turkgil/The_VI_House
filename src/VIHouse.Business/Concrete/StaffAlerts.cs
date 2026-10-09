using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Notifications;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Tells the staff who handle something that it is waiting for them: a bell notification and an
/// email to every account in the given roles, each email in that person's own language. Used for
/// influencer journal submissions and withdrawal requests. Never throws — the influencer's action
/// has already succeeded, and a mail server hiccup must not undo it.
/// </summary>
public class StaffAlerts(
    UserManager<ApplicationUser> userManager,
    INotificationService notifications,
    IEmailService emailService,
    IOptions<SiteOptions> siteOptions,
    ILogger<StaffAlerts> logger)
{
    /// <param name="adminPath">Where in the admin panel the work is, e.g. "/admin/withdrawals".</param>
    /// <param name="model">The email model, built per recipient from the absolute admin link.</param>
    public async Task SendAsync<TModel>(
        IReadOnlyCollection<string> roles, string title, string body, string adminPath,
        string emailTemplate, string emailSubject, Func<string, TModel> model,
        string relatedEntityType, Guid relatedEntityId, CancellationToken ct = default)
    {
        try
        {
            var people = new Dictionary<Guid, ApplicationUser>();
            foreach (var role in roles)
                foreach (var user in await userManager.GetUsersInRoleAsync(role))
                    people.TryAdd(user.Id, user);

            var link = SiteUrls.Absolute(siteOptions.Value.AdminBaseUrl, adminPath);
            foreach (var person in people.Values)
            {
                await notifications.CreateForUserAsync(person.Id, NotificationType.Influencer, title, body, adminPath, ct);
                if (person.Email is not null)
                    await emailService.SendAsync(emailTemplate, person.Email, emailSubject, model(link),
                        person.PreferredCulture ?? SiteCultures.Default, relatedEntityType, relatedEntityId, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not alert staff ({Roles}) about {Template} for {EntityType} {EntityId}.",
                string.Join(",", roles), emailTemplate, relatedEntityType, relatedEntityId);
        }
    }
}
