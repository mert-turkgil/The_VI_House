using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class TicketHoldConfiguration : IEntityTypeConfiguration<TicketHold>
{
    public void Configure(EntityTypeBuilder<TicketHold> builder)
    {
        builder.ToTable("TicketHolds");

        // Queried constantly by the 60s expiry sweep (ICapacityService.ReleaseExpiredHoldsAsync).
        builder.HasIndex(h => new { h.Status, h.ExpiresAt });

        builder.HasOne<TicketType>().WithMany().HasForeignKey(h => h.TicketTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Application>().WithMany().HasForeignKey(h => h.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invitation>().WithMany().HasForeignKey(h => h.InvitationId).OnDelete(DeleteBehavior.Restrict);
    }
}
