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
    Task<string?> SubscribeAsync(string? email, string culture, string? source, CancellationToken ct = default);
}
