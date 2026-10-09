using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Seminars;

namespace VIHouse.Business.Concrete;

public class MemberBenefitsService(
    IMembershipService membershipService,
    IFounderService founders,
    ISeminarService seminarService,
    ISeminarRepository seminars,
    IExperienceService experienceService,
    IExperienceRepository experiences,
    IRepository<ExperienceTranslation> experienceTranslations,
    IRepository<ExperienceMembershipAccess> membershipAccess,
    ITicketTypeRepository ticketTypes) : IMemberBenefitsService
{
    public async Task<MemberBenefits?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var membership = await membershipService.GetCurrentMembershipAsync(userId, ct);
        if (membership is null) return null;
        var entitlements = await membershipService.GetEntitlementsAsync(userId, ct);
        if (entitlements is null) return null;

        var perks = await founders.GetPerksAsync(userId, ct);
        var now = DateTimeOffset.UtcNow;

        // --- Sessions ----------------------------------------------------------------------------
        var sessionRows = new List<SessionBenefit>();
        var listed = await seminars.GetPublicListingAsync(new SeminarFilter { IncludeMembersOnly = true, UpcomingOnly = true, Take = 100 }, ct);
        foreach (var seminar in listed)
        {
            if (seminar.PriceMinor <= 0) continue; // free for everyone — not a membership benefit

            var included = seminar.IncludedWithMembership && entitlements.Sessions;
            var discount = MemberPricing.Combined(seminar.MemberDiscountPercent, perks.ExtraDiscount);
            if (!included && discount <= 0) continue;

            var access = await seminarService.GetAccessAsync(seminar, userId, ct);
            sessionRows.Add(new SessionBenefit(seminar,
                included ? BenefitKind.Included : BenefitKind.Discounted, access,
                included ? 100 : discount,
                included ? 0 : MemberPricing.Apply(seminar.PriceMinor, discount),
                seminar.PriceMinor));
        }

        // --- Experiences -------------------------------------------------------------------------
        var experienceRows = new List<ExperienceBenefit>();
        var upcoming = await experiences.FindAsync(e => e.EndAtUtc >= now && e.Status != ExperienceStatus.Draft
            && (e.Visibility == ExperienceVisibility.Public || e.Visibility == ExperienceVisibility.Members), ct);
        if (upcoming.Count > 0)
        {
            var ids = upcoming.Select(e => e.Id).ToList();
            var admittedIds = (await membershipAccess.FindAsync(a => ids.Contains(a.ExperienceId) && a.MembershipPlanId == membership.PlanId, ct))
                .Select(a => a.ExperienceId).ToHashSet();
            var tickets = (await ticketTypes.FindAsync(t => ids.Contains(t.ExperienceId), ct))
                .GroupBy(t => t.ExperienceId).ToDictionary(g => g.Key, g => g.ToList());
            // Loaded so ExperienceContent.Title can pick the reader's language (EF fixes them up
            // onto the tracked experiences).
            await experienceTranslations.FindAsync(t => ids.Contains(t.ExperienceId), ct);

            foreach (var experience in upcoming.OrderBy(e => e.StartAtUtc))
            {
                var included = admittedIds.Contains(experience.Id);
                var discount = MemberPricing.Combined(experience.MemberDiscountPercent, perks.ExtraDiscount);
                var cheapest = tickets.GetValueOrDefault(experience.Id)?.Where(t => t.PriceMinor > 0).OrderBy(t => t.PriceMinor).FirstOrDefault();
                if (!included && (discount <= 0 || cheapest is null)) continue;

                var access = await experienceService.GetAccessAsync(experience, userId, ct);
                experienceRows.Add(new ExperienceBenefit(experience,
                    included ? BenefitKind.Included : BenefitKind.Discounted, access,
                    included ? 100 : discount,
                    cheapest is null ? null : included ? 0 : MemberPricing.Apply(cheapest.PriceMinor, discount),
                    cheapest?.PriceMinor, cheapest?.Currency));
            }
        }

        return new MemberBenefits(entitlements, perks,
            sessionRows.OrderBy(s => s.Seminar.StartAtUtc ?? DateTimeOffset.MaxValue).ToList(),
            experienceRows);
    }
}
