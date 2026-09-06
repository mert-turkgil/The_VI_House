using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Membership;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ExperienceMembershipAccessConfiguration : IEntityTypeConfiguration<ExperienceMembershipAccess>
{
    public void Configure(EntityTypeBuilder<ExperienceMembershipAccess> builder)
    {
        builder.ToTable("ExperienceMembershipAccess");

        // One row per plan per experience. Ticking a box twice is a double-submit, not two grants.
        builder.HasIndex(a => new { a.ExperienceId, a.MembershipPlanId }).IsUnique();

        // Only the plan side is declared here. The experience side is the collection navigation on
        // ExperienceConfiguration, alongside the other five child collections — declaring it in both
        // places makes EF read them as two different relationships and shadow in an ExperienceId1
        // column, which is exactly what the first scaffold of this migration produced.
        //
        // Restrict rather than cascade: archiving a plan people are still using must not silently
        // withdraw their access to a room they were told they could attend. It should fail loudly.
        builder.HasOne<MembershipPlan>().WithMany()
            .HasForeignKey(a => a.MembershipPlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
