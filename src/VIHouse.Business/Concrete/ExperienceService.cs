using Microsoft.EntityFrameworkCore;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Experiences;

namespace VIHouse.Business.Concrete;

public class ExperienceService(
    IExperienceRepository experiences,
    ITicketTypeRepository ticketTypes,
    IRepository<ExperienceInclusion> inclusions,
    IRepository<ExperienceFaq> faqs) : IExperienceService
{
    public Task<List<Experience>> GetPublicListingAsync(ExperienceFilter filter, CancellationToken ct = default) =>
        experiences.GetPublicListingAsync(filter, ct);

    public Task<Experience?> GetPublicDetailBySlugAsync(string slug, CancellationToken ct = default) =>
        experiences.GetWithDetailsBySlugAsync(slug, ct);

    public Task<List<Experience>> GetUpcomingAsync(int take, CancellationToken ct = default) =>
        experiences.GetUpcomingAsync(take, ct);

    public Task<List<Experience>> GetSignatureAsync(int take, CancellationToken ct = default) =>
        experiences.GetSignatureAsync(take, ct);

    public Task<List<Experience>> GetAllForAdminAsync(CancellationToken ct = default) =>
        experiences.GetAllAsync(ct);

    public Task<Experience?> GetForAdminEditAsync(Guid id, CancellationToken ct = default) =>
        experiences.GetWithDetailsAsync(id, ct);

    public async Task<Experience> CreateAsync(Experience experience, CancellationToken ct = default)
    {
        await experiences.AddAsync(experience, ct);
        await experiences.SaveChangesAsync(ct);
        return experience;
    }

    public async Task UpdateCoreFieldsAsync(Experience updated, CancellationToken ct = default)
    {
        var existing = await experiences.GetByIdAsync(updated.Id, ct)
            ?? throw new InvalidOperationException($"Experience {updated.Id} not found.");

        existing.Title = updated.Title;
        existing.Slug = updated.Slug;
        existing.ShortSummary = updated.ShortSummary;
        existing.Description = updated.Description;
        existing.City = updated.City;
        existing.Country = updated.Country;
        existing.Venue = updated.Venue;
        existing.TimeZoneId = updated.TimeZoneId;
        existing.StartAtUtc = updated.StartAtUtc;
        existing.EndAtUtc = updated.EndAtUtc;
        existing.Capacity = updated.Capacity;
        existing.Status = updated.Status;
        existing.Visibility = updated.Visibility;
        existing.CoverImageUrl = updated.CoverImageUrl;
        existing.ApplicationOpenAt = updated.ApplicationOpenAt;
        existing.ApplicationCloseAt = updated.ApplicationCloseAt;
        existing.SalesOpenAt = updated.SalesOpenAt;
        existing.SalesCloseAt = updated.SalesCloseAt;
        existing.SeoTitle = updated.SeoTitle;
        existing.SeoDescription = updated.SeoDescription;
        existing.IsSignature = updated.IsSignature;
        existing.SortOrder = updated.SortOrder;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        // No explicit Update() call: `existing` was loaded via GetByIdAsync on this same scoped
        // DbContext, so it's already tracked — EF's change tracker detects the property
        // assignments above automatically at SaveChanges time.
        await experiences.SaveChangesAsync(ct);
    }

    public async Task<bool> TryDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await experiences.GetByIdAsync(id, ct);
        if (existing is null) return true;

        experiences.Remove(existing);
        try
        {
            await experiences.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Applications/Bookings/Payments reference this experience with Restrict delete
            // behavior on purpose (brief §98) — surface as "can't delete", not a 500.
            return false;
        }
    }

    public async Task AddTicketTypeAsync(Guid experienceId, TicketType ticketType, CancellationToken ct = default)
    {
        if (await experiences.GetByIdAsync(experienceId, ct) is null)
            throw new InvalidOperationException($"Experience {experienceId} not found.");

        // Inserted via ITicketTypeRepository.AddAsync (DbSet.Add) rather than by pushing into the
        // loaded Experience's TicketTypes navigation collection: EF Core can't reliably tell a
        // "new" entity from an "existing" one purely from graph discovery once the entity already
        // has a non-default (client-generated) Guid key set — it treated the new row as an UPDATE
        // and threw a bogus DbUpdateConcurrencyException. DbSet.Add() marks the state Added
        // unambiguously. Same reasoning applies to inclusions/FAQs below.
        ticketType.ExperienceId = experienceId;
        await ticketTypes.AddAsync(ticketType, ct);
        await ticketTypes.SaveChangesAsync(ct);
    }

    public async Task<bool> TryRemoveTicketTypeAsync(Guid experienceId, Guid ticketTypeId, CancellationToken ct = default)
    {
        var ticketType = await ticketTypes.GetByIdAsync(ticketTypeId, ct);
        if (ticketType is null || ticketType.ExperienceId != experienceId) return true;

        ticketTypes.Remove(ticketType);
        try
        {
            await ticketTypes.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Bookings/Payments/TicketHolds reference this ticket type — can't remove it once sold.
            return false;
        }
    }

    public async Task AddInclusionAsync(Guid experienceId, ExperienceInclusion inclusion, CancellationToken ct = default)
    {
        if (await experiences.GetByIdAsync(experienceId, ct) is null)
            throw new InvalidOperationException($"Experience {experienceId} not found.");

        inclusion.ExperienceId = experienceId;
        await inclusions.AddAsync(inclusion, ct);
        await inclusions.SaveChangesAsync(ct);
    }

    public async Task RemoveInclusionAsync(Guid experienceId, Guid inclusionId, CancellationToken ct = default)
    {
        var inclusion = await inclusions.GetByIdAsync(inclusionId, ct);
        if (inclusion is null || inclusion.ExperienceId != experienceId) return;

        inclusions.Remove(inclusion);
        await inclusions.SaveChangesAsync(ct);
    }

    public async Task AddFaqAsync(Guid experienceId, ExperienceFaq faq, CancellationToken ct = default)
    {
        if (await experiences.GetByIdAsync(experienceId, ct) is null)
            throw new InvalidOperationException($"Experience {experienceId} not found.");

        faq.ExperienceId = experienceId;
        await faqs.AddAsync(faq, ct);
        await faqs.SaveChangesAsync(ct);
    }

    public async Task RemoveFaqAsync(Guid experienceId, Guid faqId, CancellationToken ct = default)
    {
        var faq = await faqs.GetByIdAsync(faqId, ct);
        if (faq is null || faq.ExperienceId != experienceId) return;

        faqs.Remove(faq);
        await faqs.SaveChangesAsync(ct);
    }
}
