using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Marketing;

namespace VIHouse.Business.Concrete;

public class NotifySignupService(INotifySignupRepository signups) : INotifySignupService
{
    private static readonly EmailAddressAttribute EmailFormat = new();

    public async Task<string?> SubscribeAsync(string? email, string culture, string? source, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return "ComingSoon.Notify.Missing";

        // Stored lower case so the unique index means what it says. See EfNotifySignupRepository —
        // under the Turkish collation the address is not otherwise reliably comparable.
        var normalised = email.Trim().ToLowerInvariant();

        // Length before format: 320 is the column width, and an over-long value would otherwise
        // reach the database and come back as a truncation error rather than a message anyone can
        // act on.
        if (normalised.Length > 320 || !EmailFormat.IsValid(normalised)) return "ComingSoon.Notify.Invalid";

        var existing = await signups.FindByEmailAsync(normalised, ct);
        if (existing is not null) return "ComingSoon.Notify.Already";

        var signup = new NotifySignup
        {
            Email = normalised,
            Culture = SiteCultures.Normalise(culture),
            Source = source,
        };

        await signups.AddAsync(signup, ct);

        try
        {
            await signups.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique index caught a double submit between the Find above and this write — two
            // clicks, or two tabs. Read back what actually landed rather than reporting a failure:
            // they are on the list either way, and which of the two racing requests won is not
            // their problem. Mirrors ExperienceService.JoinWaitlistAsync.
            //
            // The failed insert is still tracked as Added on this scoped DbContext, so it is
            // detached first — otherwise anything later in the same request that saves would retry
            // the insert and throw again. JoinWaitlistAsync gets away without this only because
            // nothing follows it in that request.
            signups.Remove(signup);

            return await signups.FindByEmailAsync(normalised, ct) is null
                ? "ComingSoon.Notify.Failed"
                : "ComingSoon.Notify.Already";
        }

        return null;
    }
}
