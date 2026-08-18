using VIHouse.Entities.Referrals;

namespace VIHouse.Business.Abstract;

public interface IAmbassadorService
{
    Task<List<Ambassador>> GetAllAsync(CancellationToken ct = default);
    Task<Ambassador?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Ambassador?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Ambassador?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Reuses an existing account by email if one exists (mirrors PaymentService.ProvisionMemberAccountAsync), otherwise provisions a new one with a random never-communicated password — the caller builds the password-reset link, same split of responsibility as the checkout success page.</summary>
    Task<AmbassadorCreationResult> CreateAsync(string email, string name, string code, decimal commissionPercent, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task UpdateAsync(Ambassador updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Fire-and-forget from the /r/{code} redirect — a bad/unknown code is simply not recorded, never an error shown to the visitor.</summary>
    Task RecordVisitAsync(string code, string? utmSource, string? utmMedium, string? utmCampaign, string? utmContent, CancellationToken ct = default);

    /// <summary>
    /// Aggregate-only stats (brief §49: "customer private data ambassador'a gösterilmemelidir") —
    /// counts and revenue, never the names/emails of who was referred. Computed live from
    /// Application.ReferralCode / MembershipPayment.ReferralCode joined against Ambassador.Code,
    /// not a separately-maintained ledger, so it can never drift from the real records.
    /// </summary>
    Task<AmbassadorStats> GetStatsAsync(Guid ambassadorId, CancellationToken ct = default);
}

public record AmbassadorCreationResult(bool Success, Ambassador? Ambassador, Guid? UserId, string? Error)
{
    public static AmbassadorCreationResult Ok(Ambassador ambassador, Guid userId) => new(true, ambassador, userId, null);
    public static AmbassadorCreationResult Fail(string error) => new(false, null, null, error);
}

public record AmbassadorStats(
    int Visits,
    int Applications,
    int ApprovedApplications,
    int TicketPurchases,
    int MembershipPurchases,
    Dictionary<string, long> RevenueByCurrency,
    Dictionary<string, long> CommissionByCurrency);
