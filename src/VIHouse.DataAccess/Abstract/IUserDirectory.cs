namespace VIHouse.DataAccess.Abstract;

/// <summary>
/// Read-only queries over accounts for the admin Users screen and dashboard — searched, filtered
/// and paged in the database, roles joined in one query instead of one round trip per user.
/// </summary>
public interface IUserDirectory
{
    Task<UserDirectoryPage> SearchAsync(string? query, string? role, int page, int pageSize, CancellationToken ct = default);

    Task<UserDirectoryStats> GetStatsAsync(DateTimeOffset since, CancellationToken ct = default);
}

public record UserDirectoryRow(
    Guid UserId, string Email, string Name, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt,
    bool EmailConfirmed, bool IsLockedOut, IReadOnlyList<string> Roles, string? JobTitle,
    int ApplicationCount, int BookingCount);

public record UserDirectoryPage(IReadOnlyList<UserDirectoryRow> Rows, int Total, int Page, int PageSize)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public record UserDirectoryStats(
    int TotalUsers, int NewSince, int UnconfirmedEmail, int LockedOut, int Founders, int Members,
    IReadOnlyList<UserDirectoryRow> Newest);
