using VIHouse.Entities.Seminars;

namespace VIHouse.DataAccess.Abstract;

public interface ISeminarRepository : IRepository<Seminar>
{
    /// <summary>Slug lookup with translations and media loaded — the public detail page needs all
    /// three, and a seminar is never rendered without its copy.</summary>
    Task<Seminar?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Same graph as <see cref="GetBySlugAsync"/>, by id, for the admin editor.</summary>
    Task<Seminar?> GetWithDetailAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The seminar an uploaded asset belongs to, with its media loaded. Backs the media-streaming
    /// route, which is addressed by asset id alone: the URL then survives a slug change, which
    /// matters because those ids are baked into every &lt;img&gt; the rich text editor inserts.
    /// </summary>
    Task<Seminar?> GetByMediaIdAsync(Guid mediaId, CancellationToken ct = default);

    /// <summary>Published listing, already filtered by visibility for the caller's audience.</summary>
    Task<List<Seminar>> GetPublicListingAsync(SeminarFilter filter, CancellationToken ct = default);

    /// <summary>Every seminar, any status, newest first — the admin index.</summary>
    Task<List<Seminar>> GetAllForAdminAsync(CancellationToken ct = default);

    /// <summary>The named seminars with their copy loaded — for a member's "sessions I'm enrolled
    /// on" list, which knows the ids up front and must not pull the whole table to filter it.</summary>
    Task<List<Seminar>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>True when the slug is taken by a seminar other than <paramref name="exceptId"/>.
    /// Checked before insert/update so a clash surfaces as a field error instead of a unique-index
    /// violation surfacing as a 500.</summary>
    Task<bool> SlugExistsAsync(string slug, Guid? exceptId = null, CancellationToken ct = default);
}

/// <summary>Filter set for the public sessions listing.</summary>
public record SeminarFilter
{
    /// <summary>False for a signed-out visitor, which drops everything marked Members-only. Unlisted
    /// seminars are excluded from listings for everybody — they are reachable by direct link only.</summary>
    public bool IncludeMembersOnly { get; init; }

    /// <summary>True to keep only sessions whose sitting is still ahead of now. On-demand seminars
    /// (no start date) are always kept — there is nothing for them to be late for.</summary>
    public bool UpcomingOnly { get; init; }

    public int Skip { get; init; }
    public int Take { get; init; } = 50;
}
