using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Membership;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("Memberships");

        // Restrict, same reasoning as Booking: a membership record is a financial/history record
        // and must never vanish as a side effect of deleting an unrelated parent row.
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MembershipPlan>().WithMany().HasForeignKey(m => m.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
