using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.ToTable("WaitlistEntries");
        builder.HasIndex(w => new { w.ExperienceId, w.Position });

        // One place in the queue per person per experience. Without this, ten refreshes of the
        // public sign-up form are ten rows, and the position the tenth is told is a lie about how
        // many people are actually ahead of them. The service reads this back and reports the
        // existing position instead of failing — a duplicate is not an error, it is an answer.
        builder.HasIndex(w => new { w.ExperienceId, w.Email }).IsUnique();

        builder.Property(w => w.Email).HasMaxLength(320).IsRequired();
        builder.Property(w => w.FullName).HasMaxLength(200).IsRequired();

        builder.HasOne<Experience>().WithMany().HasForeignKey(w => w.ExperienceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TicketType>().WithMany().HasForeignKey(w => w.TicketTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
