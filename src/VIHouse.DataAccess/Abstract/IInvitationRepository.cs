using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Abstract;

public interface IInvitationRepository : IRepository<Invitation>
{
    Task<Invitation?> GetByCodeAsync(string code, CancellationToken ct = default);
}
