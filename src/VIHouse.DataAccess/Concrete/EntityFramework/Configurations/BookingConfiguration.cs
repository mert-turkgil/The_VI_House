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

        // One live booking per person per experience. SeminarEnrollment has had the equivalent index
        // since it was written and calls it "the real guard against a double enrolment"; Bookings had
        // none, which did not matter while every booking cost money and went through checkout — and
        // matters immediately now that a member can join with one click.
        //
        // Filtered on Cancelled so somebody who cancels can join again, which a bare unique index
        // would refuse.
        builder.HasIndex(b => new { b.ExperienceId, b.UserId })
            .IsUnique()
            .HasFilter("[Status] <> 2");

        // All Restrict: a Booking is a financial record and must never disappear as a side effect
        // of deleting an unrelated parent row (brief §98 backups / §194 audit expectations).
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Experience>().WithMany().HasForeignKey(b => b.ExperienceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TicketType>().WithMany().HasForeignKey(b => b.TicketTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Application>().WithMany().HasForeignKey(b => b.ApplicationId).OnDelete(DeleteBehavior.Restrict);
    }
}
