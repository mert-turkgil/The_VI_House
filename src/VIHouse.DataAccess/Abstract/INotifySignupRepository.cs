using VIHouse.Entities.Marketing;

namespace VIHouse.DataAccess.Abstract;

public interface INotifySignupRepository : IRepository<NotifySignup>
{
    /// <summary>
    /// The row this address already holds, or null. Lets a repeat sign-up be answered with "you are
    /// already on the list" rather than refused — see NotifySignupService.SubscribeAsync.
    /// </summary>
    Task<NotifySignup?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Newest first, paged. Paged rather than a GetAllAsync for the same reason the email log is:
    /// this table only ever grows, and the admin screen should not get slower every week the
    /// curtain stays up.
    /// </summary>
    Task<List<NotifySignup>> GetRecentAsync(int skip, int take, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);
}
