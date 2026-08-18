using VIHouse.Entities.Users;

namespace VIHouse.DataAccess.Abstract;

/// <summary>
/// Not IRepository&lt;T&gt;-based: Profile is keyed by UserId, not a BaseEntity.Id, since it's a 1:1
/// shadow of the Identity user rather than an independent aggregate.
/// </summary>
public interface IProfileRepository
{
    Task<Profile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Profile profile, CancellationToken ct = default);
    void Update(Profile profile);
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Member Directory search (brief §38) — only ever returns MembersOnly-visibility profiles.</summary>
    Task<List<MemberDirectoryEntry>> SearchDirectoryAsync(ProfileFilter filter, CancellationToken ct = default);

    /// <summary>Single directory entry for the profile detail page — null if not found or not MembersOnly-visible.</summary>
    Task<MemberDirectoryEntry?> GetDirectoryEntryAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Filter set for the Member Directory search (brief §38: industry/city/country + free text).</summary>
public record ProfileFilter
{
    public string? Industry { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string? Search { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = 24;
}

/// <summary>
/// Directory-facing projection joining Profile + ApplicationUser — deliberately its own type
/// rather than exposing ApplicationUser itself outside DataAccess (Profile has no C# navigation
/// property to ApplicationUser by design; see Profile.cs).
/// </summary>
public record MemberDirectoryEntry(
    Guid UserId,
    string FirstName,
    string LastName,
    string? City,
    string Country,
    string? CompanyName,
    string? JobTitle,
    string? Industry,
    string? Bio,
    string? PhotoUrl,
    string? LinkedInUrl,
    string? Interests,
    string? LookingFor,
    string? CanHelpWith);
