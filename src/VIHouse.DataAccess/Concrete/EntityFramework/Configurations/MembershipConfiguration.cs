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

        builder.Property(m => m.ProviderSubscriptionId).HasMaxLength(100);
        builder.Property(m => m.ProviderCustomerId).HasMaxLength(100);

        // The renewal and cancellation webhooks look a membership up by its subscription, so this
        // is the one query on the table that must not scan.
        builder.HasIndex(m => m.ProviderSubscriptionId);
        builder.HasIndex(m => new { m.UserId, m.Status });
    }
}
