using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Abstract;

public interface IExperienceRepository : IRepository<Experience>
{
    Task<Experience?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Eager-loads TicketTypes/ProgramDays(+Sessions)/Faqs/Inclusions/Gallery for a detail page or admin edit form.</summary>
    Task<Experience?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<Experience?> GetWithDetailsBySlugAsync(string slug, CancellationToken ct = default);

    Task<List<Experience>> GetPublicListingAsync(ExperienceFilter filter, CancellationToken ct = default);

    /// <summary>
    /// The distinct cities that currently have something publicly listed, alphabetically.
    ///
    /// Backs the listing page's city dropdown. A dropdown of real values is not a cosmetic upgrade
    /// over the free-text box it replaces: a typed filter can only ever return nothing, and "no
    /// experiences match" is indistinguishable to a visitor from "this site has no experiences".
    /// </summary>
    Task<List<string>> GetPublicCitiesAsync(CancellationToken ct = default);
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
