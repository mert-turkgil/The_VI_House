using VIHouse.Entities.Marketing;

namespace VIHouse.Business.Abstract;

public interface INotifySignupService
{
    /// <summary>
    /// Records an address to notify when the site launches.
    ///
    /// Returns a SharedResource key describing what happened, or <c>null</c> on a clean first-time
    /// sign-up — the same shape <c>IExperienceService.JoinWaitlistAsync</c> uses, so the caller
    /// renders a message without the service knowing anything about a view.
    ///
    /// An address already on the list is <em>not</em> reported as a failure: it comes back as
    /// "ComingSoon.Notify.Already", which is true, is what they wanted to know, and does not invite
    /// someone to keep resubmitting.
    /// </summary>
    /// <param name="consentText">The marketing-consent sentence the form showed, stored with the address.</param>
    Task<string?> SubscribeAsync(string? email, string culture, string? source, string consentText, CancellationToken ct = default);

    /// <summary>How many addresses a send would reach: those not yet notified, or everyone.</summary>
    Task<int> CountRecipientsAsync(bool includeAlreadyNotified, CancellationToken ct = default);

    /// <summary>
    /// Queues the announcement to every address on the list (or only those not yet told), each in
    /// the language it signed up in, and stamps NotifiedAt. Delivery goes through the outbox, so a
    /// list of thousands does not hold the admin's request open, and a mail that fails lands on the
    /// Emails &amp; SMS screen where it can be resent. Returns how many were queued, or an error.
    /// </summary>
    Task<LaunchAnnouncementResult> AnnounceAsync(
        LaunchAnnouncement message, bool includeAlreadyNotified, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Sends the announcement to one address straight away — the admin's own — so it can be
    /// checked in a real inbox before it goes to the list. Nothing on the list is touched.</summary>
    Task<LaunchAnnouncementResult> SendTestAsync(LaunchAnnouncement message, string toEmail, string culture, CancellationToken ct = default);

    Task<NotifySignup?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Takes an address off the list for good. True if it was there.</summary>
    Task<bool> LeaveAsync(Guid id, CancellationToken ct = default);
}

/// <summary>What the admin wrote. ButtonLabel/ButtonUrl are optional: without them the mail links to
/// the home page, labelled in each recipient's language.</summary>
public record LaunchAnnouncement(string Subject, string Headline, string Message, string? ButtonLabel, string? ButtonUrl);

public record LaunchAnnouncementResult(bool Ok, string Message, int Count = 0);
