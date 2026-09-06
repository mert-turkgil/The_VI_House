using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ExperienceImageConfiguration : IEntityTypeConfiguration<ExperienceImage>
{
    public void Configure(EntityTypeBuilder<ExperienceImage> builder)
    {
        builder.ToTable("ExperienceImages");
        builder.HasIndex(g => new { g.ExperienceId, g.SortOrder });

        // Url is no longer required: an uploaded image carries a StorageKey instead, and exactly one
        // of the two is set. Enforcing "at least one" in the database would need a check constraint
        // that EF cannot express portably; the service is the one writer and sets them as a pair.
        builder.Property(g => g.Url).HasMaxLength(1000);
        builder.Property(g => g.StorageKey).HasMaxLength(400);
        builder.Property(g => g.AltText).HasMaxLength(300);
    }
}
