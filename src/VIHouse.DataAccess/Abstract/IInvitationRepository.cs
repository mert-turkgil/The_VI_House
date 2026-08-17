using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Abstract;

public interface IInvitationRepository : IRepository<Invitation>
{
    Task<Invitation?> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Most recent invitation issued for an Application — admin screens use this to surface the link/status since there's no email pipeline yet.</summary>
    Task<Invitation?> GetLatestByApplicationAsync(Guid applicationId, CancellationToken ct = default);
}
