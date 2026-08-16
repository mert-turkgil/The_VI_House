using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ExperienceInclusionConfiguration : IEntityTypeConfiguration<ExperienceInclusion>
{
    public void Configure(EntityTypeBuilder<ExperienceInclusion> builder)
    {
        builder.ToTable("ExperienceInclusions");
        builder.HasIndex(i => new { i.ExperienceId, i.SortOrder });
        builder.Property(i => i.Text).HasMaxLength(500).IsRequired();
    }
}
