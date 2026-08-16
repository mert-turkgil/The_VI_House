using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");

        builder.HasIndex(b => b.BookingReference).IsUnique();
        builder.Property(b => b.BookingReference).HasMaxLength(20).IsRequired();
        builder.Property(b => b.Currency).HasMaxLength(3).IsRequired();

        // All Restrict: a Booking is a financial record and must never disappear as a side effect
        // of deleting an unrelated parent row (brief §98 backups / §194 audit expectations).
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Experience>().WithMany().HasForeignKey(b => b.ExperienceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TicketType>().WithMany().HasForeignKey(b => b.TicketTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Application>().WithMany().HasForeignKey(b => b.ApplicationId).OnDelete(DeleteBehavior.Restrict);
    }
}
