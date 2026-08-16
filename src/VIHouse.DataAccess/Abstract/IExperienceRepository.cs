using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Abstract;

public interface IExperienceRepository : IRepository<Experience>
{
    Task<Experience?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Eager-loads TicketTypes/ProgramDays(+Sessions)/Faqs/Inclusions/Gallery for a detail page or admin edit form.</summary>
    Task<Experience?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<Experience?> GetWithDetailsBySlugAsync(string slug, CancellationToken ct = default);

    Task<List<Experience>> GetPublicListingAsync(ExperienceFilter filter, CancellationToken ct = default);
    Task<List<Experience>> GetUpcomingAsync(int take, CancellationToken ct = default);
    Task<List<Experience>> GetSignatureAsync(int take, CancellationToken ct = default);
}

/// <summary>Filter set for the public Experiences listing (category/topic/format/price-style filters from the homepage filter bar).</summary>
public record ExperienceFilter
{
    public string? City { get; init; }
    public string? Country { get; init; }
    public ExperienceStatus? Status { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = 20;
}
