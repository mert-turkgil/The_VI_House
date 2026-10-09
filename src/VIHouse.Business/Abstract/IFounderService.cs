namespace VIHouse.Business.Abstract;

/// <summary>
/// The founder programme: who joined at launch, and what that earns them. The Founder role is the
/// gate for every perk; the settings (window, extra discount, early access, badge) live on the
/// site settings row and are edited from Admin › Founders.
/// </summary>
public interface IFounderService
{
    Task<FounderProgramme> GetProgrammeAsync(CancellationToken ct = default);

    Task UpdateProgrammeAsync(FounderProgramme programme, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Gives the Founder role to someone whose membership has just started, when the
    /// founder window is open at <paramref name="membershipStartedAt"/>. Idempotent and never
    /// throws — a failure here must not undo the membership that triggered it.</summary>
    Task<bool> TryGrantForMembershipAsync(Guid userId, DateTimeOffset membershipStartedAt, CancellationToken ct = default);

    /// <summary>Grants the role to every member whose first membership started before the window
    /// closes and who does not hold it yet. Returns how many were granted.</summary>
    Task<int> BackfillAsync(Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Records the date on an account an admin ticked Founder on by hand, and welcomes them.</summary>
    Task MarkGrantedManuallyAsync(Guid userId, CancellationToken ct = default);

    /// <summary>What this viewer gets. Read live (role from the database, not the cookie) so a
    /// grant takes effect on the next page, not the next sign-in.</summary>
    Task<FounderPerks> GetPerksAsync(Guid? userId, CancellationToken ct = default);

    Task<List<FounderListItem>> GetFoundersAsync(CancellationToken ct = default);
}

public record FounderProgramme(
    DateTimeOffset? WindowEndsAtUtc,
    int ExtraDiscountPercent,
    int EarlyAccessDays,
    bool BadgeEnabled)
{
    public bool IsWindowOpen(DateTimeOffset now) => WindowEndsAtUtc is { } end && now < end;
}

public record FounderPerks(bool IsFounder, int ExtraDiscountPercent, int EarlyAccessDays, bool ShowBadge)
{
    public static readonly FounderPerks None = new(false, 0, 0, false);

    /// <summary>When this viewer may first join something that opens to members at
    /// <paramref name="membersOpenAt"/>: that moment, or EarlyAccessDays before it for a Founder.</summary>
    public DateTimeOffset? OpensAtFor(DateTimeOffset? membersOpenAt) =>
        membersOpenAt is { } open && IsFounder && EarlyAccessDays > 0 ? open.AddDays(-EarlyAccessDays) : membersOpenAt;

    /// <summary>The Founder extra on top of a member discount, 0 when not a Founder.</summary>
    public int ExtraDiscount => IsFounder ? ExtraDiscountPercent : 0;
}

public record FounderListItem(Guid UserId, string Name, string Email, DateTimeOffset? FounderSince, string? PlanName);
