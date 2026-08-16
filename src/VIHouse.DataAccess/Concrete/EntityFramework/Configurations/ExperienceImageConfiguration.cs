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
        builder.Property(g => g.Url).HasMaxLength(1000).IsRequired();
    }
}
