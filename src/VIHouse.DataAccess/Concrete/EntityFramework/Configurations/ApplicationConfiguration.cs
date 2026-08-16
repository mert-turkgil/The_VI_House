using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("Applications");

        // Restrict, not Cascade: an Experience should never be deletable in a way that silently
        // wipes out Application history — admins archive/close experiences instead of deleting
        // them once applications exist against them.
        builder.HasOne<Experience>()
            .WithMany()
            .HasForeignKey(a => a.ExperienceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.ExperienceId, a.Status });
        builder.HasIndex(a => a.Email);

        builder.Property(a => a.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.LastName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Email).HasMaxLength(320).IsRequired();
        builder.Property(a => a.Country).HasMaxLength(2).IsRequired();

        builder.HasMany(a => a.Tags)
            .WithOne()
            .HasForeignKey(t => t.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
