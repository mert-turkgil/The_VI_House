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
        builder.Property(w => w.Email).HasMaxLength(320).IsRequired();
        builder.Property(w => w.FullName).HasMaxLength(200).IsRequired();

        builder.HasOne<Experience>().WithMany().HasForeignKey(w => w.ExperienceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TicketType>().WithMany().HasForeignKey(w => w.TicketTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
